using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dota2SettingsSetter.Core;

/// <summary>
/// A node in a Valve KeyValues (KV1) document: either a leaf with a string value
/// or a block with ordered children. Duplicate keys are allowed and kept in order.
/// </summary>
public sealed class KvNode
{
    public string Key { get; set; }
    public string Value { get; set; }
    public List<KvNode> Children { get; }

    public bool IsBlock => Children != null;

    public KvNode(string key, string value)
    {
        Key = key;
        Value = value;
    }

    public KvNode(string key)
    {
        Key = key;
        Children = new List<KvNode>();
    }

    public KvNode Child(string key) =>
        Children?.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));

    public string GetValue(string key)
    {
        var child = Child(key);
        return child != null && !child.IsBlock ? child.Value : null;
    }

    /// <summary>Sets a leaf value in place, or appends it when absent.</summary>
    public void SetValue(string key, string value)
    {
        var child = Child(key);
        if (child != null && !child.IsBlock)
            child.Value = value;
        else
            Children.Add(new KvNode(key, value));
    }

    public bool Remove(string key)
    {
        var child = Child(key);
        return child != null && Children.Remove(child);
    }

    public KvNode GetOrAddBlock(string key)
    {
        var child = Child(key);
        if (child != null && child.IsBlock)
            return child;
        child = new KvNode(key);
        Children.Add(child);
        return child;
    }
}

/// <summary>
/// Reads and writes Valve KeyValues text. Escape sequences are deliberately not
/// interpreted: Dota stores the backslash key as <c>"\"</c>, so treating backslash
/// literally is what lets files round-trip byte for byte.
/// </summary>
public static class KeyValues
{
    public static KvNode Parse(string text)
    {
        var tokens = Tokenize(text);
        int pos = 0;
        if (tokens.Count < 2 || tokens[0].Kind != TokenKind.String || tokens[1].Kind != TokenKind.Open)
            throw new FormatException("Expected a root key followed by '{'.");
        var root = new KvNode(tokens[0].Text);
        pos = 2;
        ParseBlock(tokens, ref pos, root);
        return root;
    }

    public static KvNode Load(string path) => Parse(File.ReadAllText(path, Encoding.UTF8));

    /// <summary>Writes in Dota's own style: tab indentation, two tabs between key and value, LF line endings.</summary>
    public static string Write(KvNode root)
    {
        var sb = new StringBuilder();
        WriteNode(sb, root, 0);
        return sb.ToString();
    }

    static void WriteNode(StringBuilder sb, KvNode node, int depth)
    {
        var indent = new string('\t', depth);
        if (!node.IsBlock)
        {
            sb.Append(indent).Append('"').Append(node.Key).Append("\"\t\t\"").Append(node.Value).Append("\"\n");
            return;
        }
        sb.Append(indent).Append('"').Append(node.Key).Append("\"\n");
        sb.Append(indent).Append("{\n");
        foreach (var child in node.Children)
            WriteNode(sb, child, depth + 1);
        sb.Append(indent).Append("}\n");
    }

    static void ParseBlock(List<Token> tokens, ref int pos, KvNode block)
    {
        while (pos < tokens.Count)
        {
            var token = tokens[pos++];
            if (token.Kind == TokenKind.Close)
                return;
            if (token.Kind != TokenKind.String)
                throw new FormatException($"Unexpected '{{' at line {token.Line}.");

            SkipConditional(tokens, ref pos);
            if (pos >= tokens.Count)
                throw new FormatException($"Key '{token.Text}' has no value (line {token.Line}).");

            var next = tokens[pos++];
            if (next.Kind == TokenKind.Open)
            {
                var child = new KvNode(token.Text);
                ParseBlock(tokens, ref pos, child);
                block.Children.Add(child);
            }
            else if (next.Kind == TokenKind.String)
            {
                block.Children.Add(new KvNode(token.Text, next.Text));
            }
            else
            {
                throw new FormatException($"Key '{token.Text}' is followed by '}}' (line {token.Line}).");
            }
            SkipConditional(tokens, ref pos);
        }
    }

    // Platform conditionals such as [$WIN32] carry no meaning for us.
    static void SkipConditional(List<Token> tokens, ref int pos)
    {
        if (pos < tokens.Count && tokens[pos].Kind == TokenKind.String && !tokens[pos].Quoted
            && tokens[pos].Text.StartsWith("[") && tokens[pos].Text.EndsWith("]"))
            pos++;
    }

    enum TokenKind { String, Open, Close }

    readonly struct Token
    {
        public readonly TokenKind Kind;
        public readonly string Text;
        public readonly bool Quoted;
        public readonly int Line;

        public Token(TokenKind kind, string text, bool quoted, int line)
        {
            Kind = kind;
            Text = text;
            Quoted = quoted;
            Line = line;
        }
    }

    static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        int i = 0, line = 1;
        if (text.Length > 0 && text[0] == '﻿')
            i = 1;

        while (i < text.Length)
        {
            char c = text[i];
            if (c == '\n')
            {
                line++;
                i++;
            }
            else if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n')
                    i++;
            }
            else if (c == '{')
            {
                tokens.Add(new Token(TokenKind.Open, "{", false, line));
                i++;
            }
            else if (c == '}')
            {
                tokens.Add(new Token(TokenKind.Close, "}", false, line));
                i++;
            }
            else if (c == '"')
            {
                int start = ++i;
                while (i < text.Length && text[i] != '"')
                {
                    if (text[i] == '\n')
                        line++;
                    i++;
                }
                if (i >= text.Length)
                    throw new FormatException($"Unterminated string starting on line {line}.");
                tokens.Add(new Token(TokenKind.String, text.Substring(start, i - start), true, line));
                i++;
            }
            else
            {
                int start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '{' && text[i] != '}' && text[i] != '"')
                    i++;
                tokens.Add(new Token(TokenKind.String, text.Substring(start, i - start), false, line));
            }
        }
        return tokens;
    }
}

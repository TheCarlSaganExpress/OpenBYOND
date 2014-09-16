﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Irony.Parsing;
using log4net;
using System.Diagnostics;

namespace OpenBYOND.VM
{
    /// <summary>
    /// Because Irony's indentation filter is dumber than a bag of bricks.
    /// 
    /// This one generates a dedent for every indentation level it drops.
    /// </summary>
    public class IndentFilter : TokenFilter
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(IndentFilter));
        public int[] indent_stack = new int[200];
        public TokenStack OutputTokens = new TokenStack();
        private ParsingContext pcontext;
        private bool readingIndents = false;
        private Grammar grammar;
        private GrammarData grammarData;
        private int current_indent_level;
        private int current_line_indent;
        private int indent_level;
        private int linenum;
        private int bracket_indent_level;
        private SourceLocation currentLoc;
        private KeyTerm opening_bracket;
        private KeyTerm closing_bracket;

        public IndentFilter(GrammarData grammarData, KeyTerm lineContinuation, KeyTerm bracket_open, KeyTerm bracket_close)
        {
            this.grammarData = grammarData;
            grammar = grammarData.Grammar;
            grammar.LanguageFlags |= LanguageFlags.EmitLineStartToken;

            opening_bracket = bracket_open;
            closing_bracket = bracket_close;
        }

        public override IEnumerable<Token> BeginFiltering(ParsingContext context, IEnumerable<Token> tokens)
        {
            this.pcontext = context;
            foreach (Token t in tokens)
            {
                currentLoc = t.Location;
                if (readingIndents)
                {
                    while (ProcessToken(t)) { ;}
                    while (OutputTokens.Count > 0)
                        yield return OutputTokens.Pop();
                }
                else
                {
                    if (t.Terminal == this.grammar.Eos)
                    {
                        /* Only handle block indents if we're not in a bracket indent. */
                        if (bracket_indent_level == 0)
                        {
                            current_line_indent = 0;
                            readingIndents = true;
                        }
                        linenum++;
                    }
                    /* Dream is dumb and permits opening/closing brackets in addition to indents */
                    if (t.Terminal == opening_bracket) { bracket_indent_level++; yield return new Token(grammar.Indent, currentLoc, string.Empty, null); continue; }
                    if (t.Terminal == closing_bracket) { Debug.Assert(bracket_indent_level > 0); bracket_indent_level--; yield return new Token(grammar.Dedent, currentLoc, string.Empty, null); continue; }
                    yield return t;
                }
            }
        }
        private void PushToken(Terminal term)
        {
            OutputTokens.Push(new Token(term, currentLoc, string.Empty, null));
        }
        private void PushToken(Token t)
        {
            OutputTokens.Push(t);
        }

        private bool ProcessToken(Token t)
        {
            if (t.Terminal == this.grammar.LineStartTerminal) return HandleLineStart(t);
            if (t.Terminal == this.grammar.Eof) return HandleEOF(t);
            if (t.Terminal == this.grammar.Eos) { current_line_indent = 0; linenum++; /*ignoring blank line */ }
            PushToken(t);
            readingIndents = false;
            return false;
        }

        private bool HandleEOF(Token t)
        {
            log.DebugFormat("CLI: {0}, CIL: {1}]", current_line_indent, current_indent_level);
            current_line_indent = 0;
            if (current_indent_level > 0)
            {
                current_indent_level--;
                PushToken(grammar.Dedent);
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool HandleLineStart(Token t)
        {
            current_line_indent = t.Location.Column;
            log.DebugFormat("CLI: {0}, CIL: {1}", current_line_indent, current_indent_level);
            if (current_line_indent > indent_level)
            {
                indent_stack[++current_indent_level] = indent_level = current_line_indent;
                PushToken(grammar.Indent);
                return true;
            }
            else if (current_line_indent < indent_level)
            {
                current_indent_level--;
                indent_level = indent_stack[current_indent_level];
                //printf(" [nIL=%d]",indent_level);
                PushToken(grammar.Dedent);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}

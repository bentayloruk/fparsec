﻿// Copyright (c) Stephan Tolksdorf 2007-2009
// License: Simplified BSD License. See accompanying documentation.

using System;
using System.Diagnostics;

namespace FParsec {

public sealed unsafe class State<TUserState> : IEquatable<State<TUserState>> {
    // splitting up the state into a fast changing part, the iterator, and a
    // a slowly changing part, the data member, improves performance
    private sealed class Data {
        public long       Line;
        public long       LineBegin;
        public TUserState UserState;
        public string     StreamName;

        public Data(long line, long lineBegin, TUserState userState, string streamName) {
            this.Line       = line;
            this.LineBegin  = lineBegin;
            this.UserState  = userState;
            this.StreamName = streamName;
        }
    }

    public CharStream.Iterator Iter;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Data data;

    private State(CharStream.Iterator iter, Data data) {
        this.Iter = iter;
        this.data = data;
    }

    public State(CharStream stream, TUserState userState) : this(stream, userState, "") { }

    public State(CharStream stream, TUserState userState, string streamName) {
        Iter = stream.Begin;
        data = new Data(1, Iter.Anchor->CharIndexOffset, userState, streamName);
    }

    public State(CharStream stream, Pos pos, TUserState userState) {
        Iter = stream.Seek(pos.Index); // will throw for negative index
        if (Iter.Index != pos.Index) throw new ArgumentOutOfRangeException("Pos.Index", "The index is too large.");
        data = new Data(pos.Line, pos.Index - pos.Column + 1, userState, pos.StreamName);
    }

    public State(CharStream.Iterator iter, long line, long column, TUserState userState)
           : this(iter, line, column, userState, "") { }

    public State(CharStream.Iterator iter, long line, long column, TUserState userState, string streamName) {
        Iter = iter;
        data = new Data(line, iter.Index - column + 1, userState, streamName);
    }

    public CharStream Stream     { get { return Iter.Stream; } }
    public string     StreamName { get { return data.StreamName; } }
    public long       Index      { get { return Iter.Index; } }
    public long       Line       { get { return data.Line; } }
    /// <summary>Is equivalent to Index - Column + 1.</summary>
    public long       LineBegin  { get { return data.LineBegin; } }
    /// <summary>The 1-based index of the UTF16 char in the line.</summary>
    public long       Column     { get { return Iter.Index - data.LineBegin + 1; } }
    public TUserState UserState  { get { return data.UserState; } }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public State<TUserState> Next { get {
        // inline Iter.Next for the common case
        var iter = Iter;
        char* newPtr = iter.Ptr + 1;
        if (iter.Block == iter.Anchor->Block && newPtr < iter.Anchor->BufferEnd) {
            iter.Ptr = newPtr;
            return new State<TUserState>(iter, data);
        } else {
            return new State<TUserState>(Iter.Next, data);
        }
    } }

    public State<TUserState> Advance(int nChars) {
        return new State<TUserState>(Iter.Advance(nChars), data);
    }
    public State<TUserState> Advance(int nChars, TUserState userState) {
        return new State<TUserState>(Iter.Advance(nChars),
                                     new Data(data.Line,
                                              data.LineBegin,
                                              userState,
                                              data.StreamName));
    }
    public State<TUserState> Advance(int nChars, int nLines, int nCharsAfterLastNL) {
        if (nLines > 0) {
            long newLineBegin = Iter.Index + nChars - nCharsAfterLastNL;
            return new State<TUserState>(Iter.Advance(nChars),
                                         new Data(data.Line + nLines,
                                                  newLineBegin,
                                                  data.UserState,
                                                  data.StreamName));
        } else return Advance(nChars);
    }
    public State<TUserState> Advance(int nChars, int nLines, int nCharsAfterLastNL, TUserState userState) {
        if (nLines > 0) {
            long newLineBegin = Iter.Index + nChars - nCharsAfterLastNL;
            return new State<TUserState>(Iter.Advance(nChars),
                                         new Data(data.Line + nLines,
                                                  newLineBegin,
                                                  userState,
                                                  data.StreamName));
        } else return Advance(nChars, userState);
    }

    internal State<TUserState> AdvanceTo(char* ptr) {
        var newState = new State<TUserState>(Iter, data);
        newState.Iter.Ptr = ptr;
        return newState;
    }
    internal State<TUserState> AdvanceTo(char* ptr, char* lineBegin) {
        var anchor = Iter.Anchor;
        var d = CharStream.PositiveDistance(anchor->BufferBegin, lineBegin);
        long newLineBegin = d + anchor->CharIndexPlusOffset;
        var newData  = new Data(data.Line + 1, newLineBegin, data.UserState, data.StreamName);
        var newState = new State<TUserState>(Iter, newData);
        newState.Iter.Ptr = ptr;
        return newState;
    }
    internal State<TUserState> AdvanceTo(char* ptr, char* lineBegin, int nLines) {
        var anchor = Iter.Anchor;
        var d = CharStream.PositiveDistance(anchor->BufferBegin, lineBegin);
        long newLineBegin = d + anchor->CharIndexPlusOffset;
        var newData = new Data(data.Line + nLines, newLineBegin, data.UserState, data.StreamName);
        var newState = new State<TUserState>(Iter, newData);
        newState.Iter.Ptr = ptr;
        return newState;
    }

    public State<TUserState> AdvanceTo(CharStream.Iterator iter) {
        return new State<TUserState>(iter, data);
    }
    public State<TUserState> AdvanceTo(CharStream.Iterator iter, TUserState userState) {
        return new State<TUserState>(iter, new Data(data.Line, data.LineBegin, userState, data.StreamName));
    }
    public State<TUserState> AdvanceTo(CharStream.Iterator iter, int nLines, int nCharsAfterLastNL) {
        if (nLines > 0) {
            return new State<TUserState>(iter, new Data(data.Line + nLines,
                                                        iter.Index - nCharsAfterLastNL,
                                                        data.UserState,
                                                        data.StreamName));
        } else return AdvanceTo(iter);
    }
    public State<TUserState> AdvanceTo(CharStream.Iterator iter, int nLines, int nCharsAfterLastNL, TUserState userState) {
        if (nLines > 0) {
            return new State<TUserState>(iter, new Data(data.Line + nLines,
                                                        iter.Index - nCharsAfterLastNL,
                                                        userState,
                                                        data.StreamName));
        } else return AdvanceTo(iter);
    }
    public State<TUserState> AdvanceTo(CharStream.Iterator iter, uint nLines, uint nCharsAfterLastNL) {
        return new State<TUserState>(iter, new Data(data.Line + nLines,
                                                    iter.Index - nCharsAfterLastNL,
                                                    data.UserState,
                                                    data.StreamName));
    }
    public State<TUserState> AdvanceTo(CharStream.Iterator iter, uint nLines, uint nCharsAfterLastNL, TUserState userState) {
        return new State<TUserState>(iter, new Data(data.Line + nLines,
                                                    iter.Index - nCharsAfterLastNL,
                                                    userState,
                                                    data.StreamName));
    }
    public State<TUserState> AdvanceTo(CharStream.Iterator iter, long nLines, long nCharsAfterLastNL) {
        if (nLines > 0) {
            return new State<TUserState>(iter, new Data(data.Line + nLines,
                                                        iter.Index - nCharsAfterLastNL,
                                                        data.UserState,
                                                        data.StreamName));
        } else return AdvanceTo(iter);
    }
    public State<TUserState> AdvanceTo(CharStream.Iterator iter, long nLines, long nCharsAfterLastNL, TUserState userState) {
        if (nLines > 0) {
            return new State<TUserState>(iter, new Data(data.Line + nLines,
                                                        iter.Index - nCharsAfterLastNL,
                                                        userState,
                                                        data.StreamName));
        } else return AdvanceTo(iter);
    }

    public State<TUserState> WithUserState(TUserState userState) {
        return new State<TUserState>(Iter, new Data(data.Line,
                                                    data.LineBegin,
                                                    userState,
                                                    data.StreamName));
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public Pos Pos { get {
        long index = Index;
        return new Pos(data.StreamName, index, data.Line, index - data.LineBegin + 1);
    } }

    public override bool Equals(object other) {
        // most of the time this and other are equal references ...
        if ((object)this == other) return true;
        var state2 = other as State<TUserState>;
        return    (object)state2 != null
               // ... or their iterator indices (computed from the the Block and and Ptr) differ
               && (Iter.Ptr == state2.Iter.Ptr || Iter.Block != state2.Iter.Block)
               && EqualsHelper(state2);
    }

    public bool Equals(State<TUserState> other) {
        return    (object)this == (object)other
               || (   (object)other != null
                   && (Iter.Ptr == other.Iter.Ptr || Iter.Block != other.Iter.Block)
                   && EqualsHelper(other));
    }

    public static bool operator==(State<TUserState> s1, State<TUserState> s2) {
        return    (object)s1 == (object)s2
               || (   (object)s1 != null && (object)s2 != null
                   && (s1.Iter.Ptr == s2.Iter.Ptr || s1.Iter.Block != s2.Iter.Block)
                   && s1.EqualsHelper(s2));
    }
    public static bool operator!=(State<TUserState> s1, State<TUserState> s2) { return !(s1 == s2); }

    private bool EqualsHelper(State<TUserState> other) {
        Data d1 = data, d2 = other.data;
        return    Iter == other.Iter
               && Object.Equals(d1.UserState, d2.UserState)
               && d1.Line == d2.Line
               && d1.LineBegin == d2.LineBegin
               && d1.StreamName == d2.StreamName;
    }

    public override int GetHashCode() {  // GetHashCode() is not required to return different hash codes for unequal instances
        return Iter.Index.GetHashCode(); // and any change in the data members usually coincides with an iterator movement
    }

    public string ReadUntil(State<TUserState> stateAfterEndOfStream) {
        string str = Iter.ReadUntil(stateAfterEndOfStream.Iter);
        if ((object)data == (object)stateAfterEndOfStream.data) return str;
        return CharStream.NormalizeNewlines(str);
    }

    public State<TUserState> SkipWhitespace() {
        char* end = Iter.Anchor->BufferEnd - 1; // - 1 to guarantee the lookahead for '\r',
        char* ptr = Iter.Ptr;
        if (Iter.Block == Iter.Anchor->Block && ptr + 2 < end) { // + 2 so that we don't need to check after the first iteration
            char* lineBegin = null;
            int nLines = 0;
            char c = *ptr;
            ++ptr;
            if (c > ' ') goto ReturnThis;
            if (c == ' ') {
                if (*ptr > ' ') return AdvanceTo(ptr);
            } else {
                if (c == '\r') {
                    if (*ptr == '\n') ++ptr;
                } else if (c != '\n') goto CheckTab;
                if (*ptr > ' ') return AdvanceTo(ptr, ptr);
                lineBegin = ptr;
                ++nLines;
                goto Loop;
            CheckTab:
                if (c != '\t') goto ReturnThis;
            }
        Loop:
            for (;;) {
                c = *ptr;
                ++ptr;
                if (c == ' ') {
                    if (ptr >= end) break;
                } else if (c == '\r') {
                    if (*ptr == '\n') ++ptr;
                    lineBegin = ptr;
                    ++nLines;
                    if (ptr >= end) break;
                } else if (c == '\n') {
                    lineBegin = ptr;
                    ++nLines;
                    if (ptr >= end) break;
                } else if (c == '\t') {
                    if (ptr >= end) break;
                } else {
                    --ptr;
                    if (nLines == 0) return AdvanceTo(ptr);
                    return AdvanceTo(ptr, lineBegin, nLines);
                }
            }
            // reached the end of the current block
            int index = CharStream.PositiveDistance(Iter.Ptr, ptr);
            var newState = nLines == 0 ? Advance(index)
                                       : Advance(index, nLines, CharStream.PositiveDistance(lineBegin, ptr));
            return newState.SkipWhitespaceContinue();
        }
        return SkipWhitespaceContinue();
    ReturnThis:
        return this;
    }
    private State<TUserState> SkipWhitespaceContinue() {
        var state = this;
        for (;;) {
            char c = state.Iter.Read();
            if (c == ' ' || c == '\t') {
                state = state.Next;
            } else if (c == '\r' || c == '\n') {
                state = state.Advance((c == '\r' && state.Iter.Peek() == '\n' ? 2 : 1), 1, 0);
            } else {
                return state;
            }
        }
    }

    public State<TUserState> SkipNewline() {
        var anchor = Iter.Anchor;
        var ptr = Iter.Ptr;
        if (Iter.Block == anchor->Block && ptr + 2 < anchor->BufferEnd) {
            char c = *ptr;
            ++ptr;
            if (c == '\r') {
                if (*ptr == '\n') ++ptr;
            } else if (c != '\n') goto ReturnThis;
            return AdvanceTo(ptr, ptr);
        } else {
            char c = Iter.Read();
            int i = 1;
            if (c == '\r') {
                if (Iter.Peek() == '\n') i = 2;
            } else if (c != '\n') goto ReturnThis;
            return Advance(i, 1, 0);
        }
    ReturnThis:
        return this;
    }

    public State<TUserState> SkipRestOfLine(bool skipNewline) {
        State<TUserState> state = this;
        char* end = Iter.Anchor->BufferEnd - 2; // - 2, so that we can do (*) without further checking
        char* ptr = Iter.Ptr;
        if (Iter.Block == Iter.Anchor->Block && ptr < end) {
            for (;;) {
                char c = *ptr;
                if (c > '\r') {
                    if (++ptr >= end) break;
                } else if (c == '\r' || c == '\n') {
                    if (!skipNewline) {
                        if (ptr != Iter.Ptr) return AdvanceTo(ptr);
                        else return this;
                    } else {
                        ++ptr;
                        if (c == '\r' && *ptr == '\n') ++ptr;
                        return AdvanceTo(ptr, ptr);  // (*)
                    }
                } else if (++ptr >= end) break;
            }
            // reached the end of the current block
            if (ptr != Iter.Ptr) {
                int count = CharStream.PositiveDistance(Iter.Ptr, ptr);
                state = Advance(count);
            }
        }
        for (;;) {
            char c = state.Iter.Read();
            if (c != CharStream.Iterator.EndOfStreamChar) {
                if (c != '\r' && c != '\n') state = state.Next;
                else {
                    if (!skipNewline) return state;
                    return state.Advance((c == '\r' && state.Iter.Peek() == '\n' ? 2 : 1), 1, 0);
                }
            } else return state;
        }
    }

    public State<TUserState> SkipRestOfLine(bool skipNewline, out string skippedString) {
        State<TUserState> state = this;
        char* end = Iter.Anchor->BufferEnd - 2; // - 2, so that we can do (*) without further checking
        char* ptr = Iter.Ptr;
        if (Iter.Block == Iter.Anchor->Block && ptr < end) {
            for (;;) {
                char c = *ptr;
                if (c > '\r') {
                    if (++ptr >= end) break;
                } else if (c == '\r' || c == '\n') {
                    char* ptr0 = Iter.Ptr;
                    if (!skipNewline) {
                        if (ptr != ptr0) {
                            int length = CharStream.PositiveDistance(ptr0, ptr);
                            skippedString = new string(ptr0, 0, length);
                            return AdvanceTo(ptr);
                        } else {
                            skippedString = "";
                            return this;
                        }
                    } else {
                        if (ptr != ptr0) {
                            int length = CharStream.PositiveDistance(ptr0, ptr);
                            skippedString = new string(ptr0, 0, length);
                        } else {
                            skippedString = "";
                        }
                        ++ptr;
                        if (c == '\r' && *ptr == '\n') ++ptr;
                        return AdvanceTo(ptr, ptr); // (*)
                    }
                } else if (++ptr >= end) break;
            }
            // reached the end of the current block
            if (ptr != Iter.Ptr) {
                int count = CharStream.PositiveDistance(Iter.Ptr, ptr);
                state = Advance(count);
            }
        }
        for (;;) {
            char c = state.Iter.Read();
            if (c != CharStream.Iterator.EndOfStreamChar) {
                if (c != '\r' && c != '\n') state = state.Next;
                else {
                    skippedString = (object)state != (object)this ? Iter.ReadUntil(state.Iter) : "";
                    if (!skipNewline) return state;
                    return state.Advance((c == '\r' && state.Iter.Peek() == '\n' ? 2 : 1), 1, 0);
                }
            } else {
                skippedString = (object)state != (object)this ? Iter.ReadUntil(state.Iter) : "";
                return state;
            }
        }
    }

    public State<TUserState> SkipCharOrNewline() {
        var anchor = Iter.Anchor;
        var ptr = Iter.Ptr;
        if (Iter.Block == anchor->Block && ptr + 2 < anchor->BufferEnd) {
            char c = *ptr;
            ++ptr;
            if (c <= '\r') goto CheckForNewline;
        SkipNormalChar:
            return AdvanceTo(ptr);
        CheckForNewline:
            if (c == '\r') {
                if (*ptr == '\n') ++ptr;
            } else if (c != '\n') goto SkipNormalChar;
      //SkipNewline:
            return AdvanceTo(ptr, ptr);
        } else {
            char c = Iter.Read();
            if (c != CharStream.Iterator.EndOfStreamChar) {
                if (c != '\r' && c != '\n') return Next;
                else return Advance(c == '\r' && Iter.Peek() == '\n' ? 2 : 1, 1, 0);
            } else return this;
        }
    }

    public State<TUserState> SkipCharsOrNewlines(int maxCharsOrNewlines, out string skippedString) {
        if (maxCharsOrNewlines < 0) throw new ArgumentOutOfRangeException("maxCharsOrNewlines is negative.");
        char* lineBegin = null;
        int nLines = 0;
        int nCRLF = 0;
        int nCR = 0;
        char* ptr = Iter.Ptr;
        var anchor = Iter.Anchor;
        if (Iter.Block == anchor->Block) {
            char* bufferEnd1 = anchor->BufferEnd - 1; // - 1 to guarantee the lookahead for '\r'
            char* end2 = unchecked (ptr + maxCharsOrNewlines); // could overflow
            char* end = end2 >= ptr && end2 <= bufferEnd1 ? end2 : bufferEnd1;
            if (ptr < end) {
                for (;;) {
                    char c = *ptr;
                    ++ptr;
                    if (c > '\r') {
                        if (ptr >= end) break;
                    } else {
                        if (c == '\r') {
                            if (*ptr == '\n') {
                                ++ptr;
                                ++nCRLF;
                                if (end < bufferEnd1) ++end;
                            } else {
                                ++nCR;
                            }
                        } else if (c != '\n') goto CheckBound;
                        lineBegin = ptr;
                        ++nLines;
                    CheckBound:
                        if (ptr >= end) break;
                    }
                }
                if (end < bufferEnd1) {
                    char* ptr0 = Iter.Ptr;
                    int index = CharStream.PositiveDistance(ptr0, ptr);
                    if (nLines == 0) {
                        skippedString = new string(ptr0, 0, index);
                        return AdvanceTo(ptr);
                    } else {
                        if ((nCR | nCRLF) == 0)
                            skippedString = new string(ptr0, 0, index);
                        else
                            skippedString = CharStream.CopyWithNormalizedNewlines(ptr0, index, nCRLF, nCR);
                        return AdvanceTo(ptr, lineBegin, nLines);
                    }
                }
            }
        }
        return SkipCharsOrNewlinesContinue(maxCharsOrNewlines, out skippedString, ptr, lineBegin, nLines, nCRLF, nCR);
    }
    private State<TUserState> SkipCharsOrNewlinesContinue(int maxCharsOrNewlines, out string skippedString,
                                                         char* ptr, char* lineBegin, int nLines, int nCRLF, int nCR)
    {
        int index = CharStream.PositiveDistance(Iter.Ptr, ptr);
        int count = index - nCRLF;
        int lineBeginCount = nLines == 0 ? 0 : count - CharStream.PositiveDistance(lineBegin, ptr);
        CharStream.Iterator iter = Iter;
        char c = iter._Increment((uint)index);
        for (;;) {
             if (c == CharStream.Iterator.EndOfStreamChar || count == maxCharsOrNewlines) break;
             ++count;
             char c1 = iter._Increment();
             if (c > '\r') c = c1;
             else if (c == '\r') {
                 if (c1 == '\n') {
                     ++nCRLF;
                     c = iter._Increment();
                 } else {
                     ++nCR;
                     c = c1;
                 }
                 lineBeginCount = count;
                 ++nLines;
             } else if (c == '\n') {
                 c = c1;
                 lineBeginCount = count;
                 ++nLines;
             } else c = c1;
        }
        if (count != 0) {
            string s = Iter.ReadUntil(iter);
            if (nLines == 0) {
                skippedString = s;
                return AdvanceTo(iter);
            } else {
                if ((nCR | nCRLF) == 0) skippedString = s;
                else {
                    fixed (char* ps = s)
                        skippedString = CharStream.CopyWithNormalizedNewlines(ps, count + nCRLF, nCRLF, nCR);
                }
                return AdvanceTo(iter, nLines, count - lineBeginCount);
            }
        } else {
            skippedString = "";
            return this;
        }
    }

    public State<TUserState> SkipCharsOrNewlines(int maxCharsOrNewlines, out int numberOfSkippedCharsOrNewlines) {
        if (maxCharsOrNewlines < 0) throw new ArgumentOutOfRangeException("maxCharsOrNewlines is negative.");
        char* lineBegin = null;
        int nLines = 0;
        int nCRLF = 0;
        char* ptr = Iter.Ptr;
        var anchor = Iter.Anchor;
        if (Iter.Block == anchor->Block) {
            char* bufferEnd1 = anchor->BufferEnd - 1; // - 1 to guarantee the lookahead for '\r'
            char* end2 = unchecked (ptr + maxCharsOrNewlines); // could overflow
            char* end = end2 >= ptr && end2 <= bufferEnd1 ? end2 : bufferEnd1;
            if (ptr < end) {
                for (;;) {
                    char c = *ptr;
                    ++ptr;
                    if (c > '\r') {
                        if (ptr >= end) break;
                    } else {
                        if (c == '\r') {
                            if (*ptr == '\n') {
                                ++ptr;
                                ++nCRLF;
                                if (end < bufferEnd1) ++end;
                            }
                        } else if (c != '\n') goto CheckBound;
                        lineBegin = ptr;
                        ++nLines;
                    CheckBound:
                        if (ptr >= end) break;
                    }
                }
                if (end < bufferEnd1) {
                    numberOfSkippedCharsOrNewlines = CharStream.PositiveDistance(Iter.Ptr, ptr) - nCRLF;
                    if (nLines == 0) return AdvanceTo(ptr);
                    else return AdvanceTo(ptr, lineBegin, nLines);
                }
            }
        }
        return SkipCharsOrNewlinesContinue(maxCharsOrNewlines, out numberOfSkippedCharsOrNewlines, ptr, lineBegin, nLines, nCRLF);
    }
    private State<TUserState> SkipCharsOrNewlinesContinue(int maxCharsOrNewlines, out int numberOfSkippedCharsOrNewlines,
                                                         char* ptr, char* lineBegin, int nLines, int nCRLF)
    {
        int index = CharStream.PositiveDistance(Iter.Ptr, ptr);
        int count = index - nCRLF;
        int lineBeginCount = nLines == 0 ? 0 : count - CharStream.PositiveDistance(lineBegin, ptr);
        CharStream.Iterator iter = Iter;
        char c = iter._Increment((uint)index);
        for (;;) {
             if (c == CharStream.Iterator.EndOfStreamChar || count == maxCharsOrNewlines) break;
             ++count;
             char c1 = iter._Increment();
             if (c > '\r') c = c1;
             else if (c == '\r') {
                 if (c1 == '\n') {
                     ++nCRLF;
                     c = iter._Increment();
                 } else {
                     c = c1;
                 }
                 lineBeginCount = count;
                 ++nLines;
             } else if (c == '\n') {
                 c = c1;
                 lineBeginCount = count;
                 ++nLines;
             } else c = c1;
        }
        numberOfSkippedCharsOrNewlines = count;
        if (count != 0) {
            if (nLines == 0) return AdvanceTo(iter);
            else return AdvanceTo(iter, nLines, count - lineBeginCount);
        } else return this;
    }

    public State<TUserState> SkipCharsOrNewlinesWhile(Microsoft.FSharp.Core.FastFunc<char,bool> f1, Microsoft.FSharp.Core.FastFunc<char,bool> f) {
        char* lineBegin = null;
        int nLines = 0;
        char* ptr = Iter.Ptr;
        char* end = Iter.Anchor->BufferEnd - 1; // - 1 to guarantee the lookahead for '\r'
        if (Iter.Block == Iter.Anchor->Block && ptr + 2 < end) { // + 2 so that we don't need to check after the first iteration
            char c = *ptr;
            ++ptr;
            if (c > '\r') {
                if (!f1.Invoke(c)) goto ReturnEmpty;
            } else if (c == '\r') {
                if (!f1.Invoke('\n')) goto ReturnEmpty;
                if (*ptr == '\n') ++ptr;
                lineBegin = ptr;
                ++nLines;
            } else if (c == '\n') {
                if (!f1.Invoke('\n')) goto ReturnEmpty;
                lineBegin = ptr;
                ++nLines;
            } else if (!f1.Invoke(c)) goto ReturnEmpty;
            for (;;) {
                c = *ptr;
                ++ptr;
                if (c > '\r') {
                    if (f.Invoke(c)) {
                        if (ptr >= end) break; else continue;
                    }
                } else if (c == '\r') {
                    if (f.Invoke('\n')) {
                        if (*ptr == '\n') ++ptr;
                        lineBegin = ptr;
                        ++nLines;
                        if (ptr >= end) break; else continue;
                    }
                } else if (c == '\n') {
                    if (f.Invoke('\n')) {
                        lineBegin = ptr;
                        ++nLines;
                        if (ptr >= end) break; else continue;
                    }
                } else if (f.Invoke(c)) {
                    if (ptr >= end) break; else continue;
                }
                --ptr;
                if (nLines == 0) return AdvanceTo(ptr);
                else return AdvanceTo(ptr, lineBegin, nLines);
            }
            // reached the end of the current block
            int index = CharStream.PositiveDistance(Iter.Ptr, ptr);
            var state =   nLines == 0
                        ? Advance(index)
                        : Advance(index, nLines, CharStream.PositiveDistance(lineBegin, ptr));
            return SkipCharsOrNewlinesWhileContinue(f /* not f1 */, f, state);
        ReturnEmpty:
            return this;
        }
        return SkipCharsOrNewlinesWhileContinue(f1, f, this);
    }
    private State<TUserState> SkipCharsOrNewlinesWhileContinue(Microsoft.FSharp.Core.FastFunc<char,bool> f1, Microsoft.FSharp.Core.FastFunc<char,bool> f, State<TUserState> state) {
        var ff = f1;
        for (;;) {
            char c = state.Iter.Read();
            if (c == CharStream.Iterator.EndOfStreamChar) break;
            if (c != '\r' && c != '\n') {
                if (!ff.Invoke(c)) break;
                state = state.Next;
            } else {
                if (!ff.Invoke('\n')) break;
                state = state.Advance((c == '\r' && state.Iter.Peek() == '\n' ? 2 : 1), 1, 0);
            }
            ff = f;
        }
        return state;
    }

    public State<TUserState> SkipCharsOrNewlinesWhile(Microsoft.FSharp.Core.FastFunc<char,bool> f1, Microsoft.FSharp.Core.FastFunc<char,bool> f, out string skippedString) {
        char* lineBegin = null;
        int nLines = 0;
        int nCRLF = 0;
        int nCR = 0;
        char* ptr = Iter.Ptr;
        char* end = Iter.Anchor->BufferEnd - 1; // - 1 to guarantee the lookahead for '\r'
        if (Iter.Block == Iter.Anchor->Block && ptr + 2 < end) { // + 2 so that we don't need to check after the first iteration
            char c = *ptr;
            ++ptr;
            if (c > '\r') {
                if (!f1.Invoke(c)) goto ReturnEmpty;
            } else if (c == '\r') {
                if (!f1.Invoke('\n')) goto ReturnEmpty;
                if (*ptr == '\n') {
                    ++ptr;
                    ++nCRLF;
                } else {
                    ++nCR;
                }
                lineBegin = ptr;
                ++nLines;
            } else if (c == '\n') {
                if (!f1.Invoke('\n')) goto ReturnEmpty;
                lineBegin = ptr;
                ++nLines;
            } else if (!f1.Invoke(c)) goto ReturnEmpty;
            for (;;) {
                c = *ptr;
                ++ptr;
                if (c > '\r') {
                    if (f.Invoke(c)) {
                        if (ptr >= end) break; else continue;
                    }
                } else if (c == '\r') {
                    if (f.Invoke('\n')) {
                        if (*ptr == '\n') {
                            ++ptr;
                            ++nCRLF;
                        } else {
                            ++nCR;
                        }
                        lineBegin = ptr;
                        ++nLines;
                        if (ptr >= end) break; else continue;
                    }
                } else if (c == '\n') {
                    if (f.Invoke('\n')) {
                        lineBegin = ptr;
                        ++nLines;
                        if (ptr >= end) break; else continue;
                    }
                } else if (f.Invoke(c)) {
                    if (ptr >= end) break; else continue;
                }
                --ptr;
                char* ptr0 = Iter.Ptr;
                int index = CharStream.PositiveDistance(ptr0, ptr);
                if (nLines == 0) {
                    skippedString = new string(ptr0, 0, index);
                    return AdvanceTo(ptr);
                } else {
                    if ((nCR | nCRLF) == 0)
                        skippedString = new string(ptr0, 0, index);
                    else
                        skippedString = CharStream.CopyWithNormalizedNewlines(ptr0, index, nCRLF, nCR);
                    return AdvanceTo(ptr, lineBegin, nLines);
                }
            }
            {
                // reached the end of the current block
                int index = CharStream.PositiveDistance(Iter.Ptr, ptr);
                var state = nLines == 0
                            ? Advance(index)
                            : Advance(index, nLines, CharStream.PositiveDistance(lineBegin, ptr));
                return SkipCharsOrNewlinesWhileContinue(f /* not f1 */, f, out skippedString, state);
            }
        ReturnEmpty:
            skippedString = "";
            return this;
        }
        return SkipCharsOrNewlinesWhileContinue(f1, f, out skippedString, this);
    }
    private State<TUserState> SkipCharsOrNewlinesWhileContinue(Microsoft.FSharp.Core.FastFunc<char,bool> f1, Microsoft.FSharp.Core.FastFunc<char,bool> f, out string skippedString, State<TUserState> state) {
        var ff = f1;
        for (;;) {
            char c = state.Iter.Read();
            if (c == CharStream.Iterator.EndOfStreamChar) break;
            if (c != '\r' && c != '\n') {
                if (!ff.Invoke(c)) break;
                state = state.Next;
            } else {
                if (!ff.Invoke('\n')) break;
                state = state.Advance((c == '\r' && state.Iter.Peek() == '\n' ? 2 : 1), 1, 0);
            }
            ff = f;
        }
        if ((object)state != (object)this) {
            skippedString = ReadUntil(state);
            return state;
        } else {
            skippedString = "";
            return this;
        }
    }

    public State<TUserState> SkipCharsOrNewlinesWhile(Microsoft.FSharp.Core.FastFunc<char,bool> f1, Microsoft.FSharp.Core.FastFunc<char,bool> f, int minCharsOrNewlines, int maxCharsOrNewlines) {
        if (maxCharsOrNewlines < 0) throw new ArgumentOutOfRangeException("maxCharsOrNewlines is negative.");
        char* lineBegin = null;
        int nLines = 0;
        int nCRLF = 0;
        char* ptr = Iter.Ptr;
        char* bufferEnd1 = Iter.Anchor->BufferEnd - 1; // - 1 to guarantee the lookahead for '\r'
        char* end2 = unchecked (ptr + maxCharsOrNewlines); // could overflow
        char* end = end2 >= ptr && end2 <= bufferEnd1 ? end2 : bufferEnd1;
        if (Iter.Block == Iter.Anchor->Block && ptr + 2 < end) { // + 2 so that we don't need to check after the first iteration
            char c = *ptr;
            ++ptr;
            if (c > '\r') {
                if (!f1.Invoke(c)) goto ReturnEmpty;
            } else if (c == '\r') {
                if (!f1.Invoke('\n')) goto ReturnEmpty;
                if (*ptr == '\n') {
                    ++ptr;
                    ++nCRLF;
                    if (end < bufferEnd1) ++end;
                }
                lineBegin = ptr;
                ++nLines;
            } else if (c == '\n') {
                if (!f1.Invoke('\n')) goto ReturnEmpty;
                lineBegin = ptr;
                ++nLines;
            } else if (!f1.Invoke(c)) goto ReturnEmpty;
            for (;;) {
                c = *ptr;
                ++ptr;
                if (c > '\r') {
                    if (f.Invoke(c)) {
                        if (ptr >= end) break; else continue;
                    }
                } else if (c == '\r') {
                    if (f.Invoke('\n')) {
                        if (*ptr == '\n') {
                            ++ptr;
                            ++nCRLF;
                            if (end < bufferEnd1) ++end;
                        }
                        lineBegin = ptr;
                        ++nLines;
                        if (ptr >= end) break; else continue;
                    }
                } else if (c == '\n') {
                    if (f.Invoke('\n')) {
                        lineBegin = ptr;
                        ++nLines;
                        if (ptr >= end) break; else continue;
                    }
                } else if (f.Invoke(c)) {
                    if (ptr >= end) break; else continue;
                }
                --ptr;
                goto ReturnStringInBlock;
            }
            if (end >= bufferEnd1) goto EndOfBlock;
        ReturnStringInBlock:
            {
                int count = CharStream.PositiveDistance(Iter.Ptr, ptr) - nCRLF;
                if (count >= minCharsOrNewlines) {
                    if (nLines == 0) return AdvanceTo(ptr);
                    else return AdvanceTo(ptr, lineBegin, nLines);
                }
            }
        ReturnEmpty:
            return this;
        EndOfBlock:
            {
                int index = CharStream.PositiveDistance(Iter.Ptr, ptr);
                var state = nLines == 0
                            ? Advance(index)
                            : Advance(index, nLines, CharStream.PositiveDistance(lineBegin, ptr));
                int count = index - nCRLF;
                return SkipCharsOrNewlinesWhileContinue(f /* not f1 */, f, minCharsOrNewlines, maxCharsOrNewlines, state, count);
            }
        }
        return SkipCharsOrNewlinesWhileContinue(f1, f, minCharsOrNewlines, maxCharsOrNewlines, this, 0);
    }
    private State<TUserState> SkipCharsOrNewlinesWhileContinue(Microsoft.FSharp.Core.FastFunc<char,bool> f1, Microsoft.FSharp.Core.FastFunc<char,bool> f, int minCharsOrNewlines, int maxCharsOrNewlines, State<TUserState> state, int count) {
        var ff = f1;
        for (; count < maxCharsOrNewlines; ++count) {
            char c = state.Iter.Read();
            if (c == CharStream.Iterator.EndOfStreamChar) break;
            if (c != '\r' && c != '\n') {
                if (!ff.Invoke(c)) break;
                state = state.Next;
            } else {
                if (!ff.Invoke('\n')) break;
                state = state.Advance((c == '\r' && state.Iter.Peek() == '\n' ? 2 : 1), 1, 0);
            }
            ff = f;
        }
        if (count >= minCharsOrNewlines) return state;
        else return this;
    }

    public State<TUserState> SkipCharsOrNewlinesWhile(Microsoft.FSharp.Core.FastFunc<char,bool> f1, Microsoft.FSharp.Core.FastFunc<char,bool> f, int minCharsOrNewlines, int maxCharsOrNewlines, out string skippedString) {
        if (maxCharsOrNewlines < 0) throw new ArgumentOutOfRangeException("maxCharsOrNewlines is negative.");
        char* lineBegin = null;
        int nLines = 0;
        int nCRLF = 0;
        int nCR = 0;
        char* ptr = Iter.Ptr;
        char* bufferEnd1 = Iter.Anchor->BufferEnd - 1; // - 1 to guarantee the lookahead for '\r'
        char* end2 = unchecked (ptr + maxCharsOrNewlines); // could overflow
        char* end = end2 >= ptr && end2 <= bufferEnd1 ? end2 : bufferEnd1;
        if (Iter.Block == Iter.Anchor->Block && ptr + 2 < end) { // + 2 so that we don't need to check after the first iteration
            char c = *ptr;
            ++ptr;
            if (c > '\r') {
                if (!f1.Invoke(c)) goto ReturnEmpty;
            } else if (c == '\r') {
                if (!f1.Invoke('\n')) goto ReturnEmpty;
                if (*ptr == '\n') {
                    ++ptr;
                    ++nCRLF;
                    if (end < bufferEnd1) ++end;
                } else {
                    ++nCR;
                }
                lineBegin = ptr;
                ++nLines;
            } else if (c == '\n') {
                if (!f1.Invoke('\n')) goto ReturnEmpty;
                lineBegin = ptr;
                ++nLines;
            } else if (!f1.Invoke(c)) goto ReturnEmpty;
            for (;;) {
                c = *ptr;
                ++ptr;
                if (c > '\r') {
                    if (f.Invoke(c)) {
                        if (ptr >= end) break; else continue;
                    }
                } else if (c == '\r') {
                    if (f.Invoke('\n')) {
                        if (*ptr == '\n') {
                            ++ptr;
                            ++nCRLF;
                            if (end < bufferEnd1) ++end;
                        } else {
                            ++nCR;
                        }
                        lineBegin = ptr;
                        ++nLines;
                        if (ptr >= end) break; else continue;
                    }
                } else if (c == '\n') {
                    if (f.Invoke('\n')) {
                        lineBegin = ptr;
                        ++nLines;
                        if (ptr >= end) break; else continue;
                    }
                } else if (f.Invoke(c)) {
                    if (ptr >= end) break; else continue;
                }
                --ptr;
                goto ReturnStringInBlock;
            }
            if (end >= bufferEnd1) goto EndOfBlock;
        ReturnStringInBlock:
            {
                char* ptr0 = Iter.Ptr;
                int index = CharStream.PositiveDistance(ptr0, ptr);
                int count = index - nCRLF;
                if (count >= minCharsOrNewlines) {
                    if (nLines == 0) {
                        skippedString = new string(ptr0, 0, index);
                        return AdvanceTo(ptr);
                    } else {
                        if ((nCR | nCRLF) == 0)
                            skippedString = new string(ptr0, 0, index);
                        else
                            skippedString = CharStream.CopyWithNormalizedNewlines(ptr0, index, nCRLF, nCR);
                        return AdvanceTo(ptr, lineBegin, nLines);
                    }
                }
            }
        ReturnEmpty:
            skippedString = "";
            return this;
        EndOfBlock:
            {
                int index = CharStream.PositiveDistance(Iter.Ptr, ptr);
                var state = nLines == 0
                            ? Advance(index)
                            : Advance(index, nLines, CharStream.PositiveDistance(lineBegin, ptr));
                int count = index - nCRLF;
                return SkipCharsOrNewlinesWhileContinue(f /* not f1 */, f, minCharsOrNewlines, maxCharsOrNewlines, out skippedString, state, count);
            }
        }
        return SkipCharsOrNewlinesWhileContinue(f1, f, minCharsOrNewlines, maxCharsOrNewlines, out skippedString, this, 0);
    }
    private State<TUserState> SkipCharsOrNewlinesWhileContinue(Microsoft.FSharp.Core.FastFunc<char,bool> f1, Microsoft.FSharp.Core.FastFunc<char,bool> f, int minCharsOrNewlines, int maxCharsOrNewlines, out string skippedString, State<TUserState> state, int count) {
        var ff = f1;
        for (; count < maxCharsOrNewlines; ++count) {
            char c = state.Iter.Read();
            if (c == CharStream.Iterator.EndOfStreamChar) break;
            if (c != '\r' && c != '\n') {
                if (!ff.Invoke(c)) break;
                state = state.Next;
            } else {
                if (!ff.Invoke('\n')) break;
                state = state.Advance((c == '\r' && state.Iter.Peek() == '\n' ? 2 : 1), 1, 0);
            }
            ff = f;
        }
        if (count >= minCharsOrNewlines && count != 0) {
            skippedString = ReadUntil(state);
            return state;
        } else {
            skippedString = "";
            return this;
        }
    }

    private static bool RestOfStringEquals(char* p1, char* p2, int length) {
        for (int i = 1; i < length; ++i) {
            if (p1[i] != p2[i]) goto ReturnFalse;
        }
        return true;
    ReturnFalse:
        return false;
    }
    private static bool Rest3OfStringEquals(char* p1, char* p2, int length) {
        for (int i = 3; i < length; ++i) {
            if (p1[i] != p2[i]) goto ReturnFalse;
        }
        return true;
    ReturnFalse:
        return false;
    }

    private static bool RestOfStringEqualsCI(char* p1, char* p2, int length) {
        char* cftable = CaseFoldTable.FoldedChars;
        for (int i = 1; i < length; ++i) {
            if (cftable[p1[i]] != p2[i]) goto ReturnFalse;
        }
        return true;
    ReturnFalse:
        return false;
    }

    public State<TUserState> SkipToString(string str, int maxCharsOrNewlines, out bool foundString) {
        int strLength = str.Length; // throws if str is null
        if (strLength == 0) throw new ArgumentException("The string argument is empty.");
        if (maxCharsOrNewlines < 0) throw new ArgumentOutOfRangeException("maxCharsOrNewlines is negative.");
        fixed (char* pstr = str) {
            char* lineBegin = null;
            int nLines = 0;
            int nCRLF = 0;
            char first = pstr[0];
            char* ptr = Iter.Ptr;
            char* bufferEnd = Iter.Anchor->BufferEnd;
            char* end1 = unchecked (bufferEnd - strLength);
            if (end1 >= ptr && end1 < bufferEnd) {
                char* end2 = unchecked (ptr + maxCharsOrNewlines);
                char* end = end1 <= end2 || end2 < ptr ? end1 : end2;
                if (Iter.Block == Iter.Anchor->Block && ptr < end) {
                    for (;;) {
                        char c = *ptr;
                        if (c == first) goto CompareRestOfString;
                    StringsNotEqual:
                        ++ptr;
                        if (c > '\r') {
                            if (ptr >= end) break;
                        } else {
                            if (c == '\r') {
                                if (*ptr == '\n') {
                                    ++ptr;
                                    ++nCRLF;
                                    if (end < end1) ++end;
                                }
                            } else if (c != '\n') goto CheckBound;
                            lineBegin = ptr;
                            ++nLines;
                        CheckBound:
                            if (ptr >= end) break;
                        }
                        continue;
                    CompareRestOfString:
                        if (strLength == 1 || (ptr[1] == pstr[1] &&
                            (strLength == 2 || (ptr[2] == pstr[2] &&
                             (strLength == 3 || Rest3OfStringEquals(ptr, pstr, strLength)))))) goto Found;
                        goto StringsNotEqual;
                    } // for
                    if (*ptr == first && RestOfStringEquals(ptr, pstr, strLength)) goto Found;
                    if (ptr >= end1) goto EndOfBlock;
                    foundString = false;
                    goto ReturnState;
                Found:
                    foundString = true;
                ReturnState:
                    if (ptr != Iter.Ptr) {
                        if (nLines == 0) return AdvanceTo(ptr);
                        else return AdvanceTo(ptr, lineBegin, nLines);
                    } else return this;
                }
            }
        EndOfBlock:
            return SkipToStringContinue(pstr, strLength, maxCharsOrNewlines, out foundString, ptr, lineBegin, nLines, nCRLF);
        }
    }
    private State<TUserState> SkipToStringContinue(char* pstr, int strLength, int maxCharsOrNewlines, out bool foundString,
                                                   char* ptr, char* lineBegin, int nLines, int nCRLF)
    {
        foundString = false;
        char first = *pstr;
        int index = CharStream.PositiveDistance(Iter.Ptr, ptr);
        int count = index - nCRLF;
        int lineBeginCount = nLines == 0 ? 0 : count - CharStream.PositiveDistance(lineBegin, ptr);
        CharStream.Iterator iter = Iter;
        char c = iter._Increment((uint)index);
        for (;;) {
            if (c != first || !iter.Match(pstr, strLength)) {
                if (c == CharStream.Iterator.EndOfStreamChar || count == maxCharsOrNewlines) break;
                ++count;
                char c1 = iter._Increment();
                if (c > '\r') c = c1;
                else if (c == '\r') {
                    if (c1 == '\n') {
                        ++nCRLF;
                        c = iter._Increment();
                    } else {
                        c = c1;
                    }
                    lineBeginCount = count;
                    ++nLines;
                } else if (c == '\n') {
                    c = c1;
                    lineBeginCount = count;
                    ++nLines;
                } else c = c1;
            } else {
                foundString = true;
                break;
            }
        }
        if (count != 0) {
            if (nLines == 0) return AdvanceTo(iter);
            else return AdvanceTo(iter, nLines, count - lineBeginCount);
        } else return this;
    }

    public State<TUserState> SkipToString(string str, int maxCharsOrNewlines, out string skippedString) {
        int strLength = str.Length; // throws if str is null
        if (strLength == 0) throw new ArgumentException("The string argument is empty.");
        if (maxCharsOrNewlines < 0) throw new ArgumentOutOfRangeException("maxCharsOrNewlines is negative.");
        fixed (char* pstr = str) {
            char* lineBegin = null;
            int nLines = 0;
            int nCRLF = 0;
            int nCR = 0;
            char first = pstr[0];
            char* ptr = Iter.Ptr;
            char* bufferEnd = Iter.Anchor->BufferEnd;
            char* end1 = unchecked (bufferEnd - strLength); // could overflow
            if (end1 >= ptr && end1 < bufferEnd) {
                char* end2 = unchecked (ptr + maxCharsOrNewlines); // could overflow
                char* end = end1 <= end2 || end2 < ptr ? end1 : end2;
                if (Iter.Block == Iter.Anchor->Block && ptr < end) {
                    for (;;) {
                        char c = *ptr;
                        if (c == first) goto CompareRestOfString;
                    StringsNotEqual:
                        ++ptr;
                        if (c > '\r') {
                            if (ptr >= end) break;
                        } else {
                            if (c == '\r') {
                                if (*ptr == '\n') {
                                    ++ptr;
                                    ++nCRLF;
                                    if (end < end1) ++end;
                                } else {
                                    ++nCR;
                                }
                            } else if (c != '\n') goto CheckBound;
                            lineBegin = ptr;
                            ++nLines;
                        CheckBound:
                            if (ptr >= end) break;
                        }
                        continue;
                    CompareRestOfString:
                        if (strLength == 1 || (ptr[1] == pstr[1] &&
                             (strLength == 2 || (ptr[2] == pstr[2] &&
                              (strLength == 3 || Rest3OfStringEquals(ptr, pstr, strLength)))))) goto Found;
                        goto StringsNotEqual;
                    } // for
                    if (*ptr == first && RestOfStringEquals(ptr, pstr, strLength)) goto Found;
                    if (ptr >= end1) goto EndOfBlock;
                    goto NotFound;
                Found:
                    {
                        char* ptr0 = Iter.Ptr;
                        if (ptr != ptr0) {
                            int length = CharStream.PositiveDistance(ptr0, ptr);
                            if (nLines == 0) {
                                skippedString = new string(ptr0, 0, length);
                                return AdvanceTo(ptr);
                            } else {
                                skippedString = (nCR | nCRLF) == 0
                                                ? new string(ptr0, 0, length)
                                                : CharStream.CopyWithNormalizedNewlines(ptr0, length, nCRLF, nCR);
                                return AdvanceTo(ptr, lineBegin, nLines);
                            }
                        } else {
                            skippedString = "";
                            return this;
                        }
                    }
                NotFound:
                    {
                        skippedString = null;
                        char* ptr0 = Iter.Ptr;
                        if (ptr != ptr0) {
                            int length = CharStream.PositiveDistance(ptr0, ptr);
                            if (nLines == 0) return AdvanceTo(ptr);
                            else return AdvanceTo(ptr, lineBegin, nLines);
                        } else return this;
                    }
                }
            }
        EndOfBlock:
            return SkipToStringContinue(pstr, strLength, maxCharsOrNewlines, out skippedString, ptr, lineBegin, nLines, nCRLF, nCR);
        }
    }
    private State<TUserState> SkipToStringContinue(char* pstr, int strLength, int maxCharsOrNewlines, out string skippedString,
                                                   char* ptr, char* lineBegin, int nLines, int nCRLF, int nCR)
    {
        char first = *pstr;
        CharStream.Iterator iter = Iter;
        int index = CharStream.PositiveDistance(Iter.Ptr, ptr);
        int count = index - nCRLF;
        int lineBeginCount = nLines == 0 ? 0 : count - CharStream.PositiveDistance(lineBegin, ptr);
        char c = iter._Increment((uint)index);
        for (;;) {
            if (c != first || !iter.Match(pstr, strLength)) {
                if (c == CharStream.Iterator.EndOfStreamChar || count == maxCharsOrNewlines) break;
                ++count;
                char c1 = iter._Increment();
                if (c > '\r') c = c1;
                else if (c == '\r') {
                    if (c1 == '\n') {
                        ++nCRLF;
                        c = iter._Increment();
                    } else {
                        ++nCR;
                        c = c1;
                    }
                    lineBeginCount = count;
                    ++nLines;
                } else if (c == '\n') {
                    c = c1;
                    lineBeginCount = count;
                    ++nLines;
                } else c = c1;
            } else { // found string
                if (count != 0) {
                    if (nLines == 0) {
                        skippedString = Iter.ReadUntil(iter);
                        return AdvanceTo(iter);
                    } else {
                        string s = Iter.ReadUntil(iter);
                        if ((nCR | nCRLF) == 0) skippedString = s;
                        else {
                            fixed (char* ptr0 = s)
                                skippedString = CharStream.CopyWithNormalizedNewlines(ptr0, count + nCRLF, nCRLF, nCR);
                        }
                        return AdvanceTo(iter, nLines, count - lineBeginCount);
                    }
                } else {
                    skippedString = "";
                    return this;
                }
            }
        }
        skippedString = null;
        if (count != 0) {
            if (nLines == 0) return AdvanceTo(iter);
            else return AdvanceTo(iter, nLines, count - lineBeginCount);
        } else return this;
    }

    public State<TUserState> SkipToStringCI(string caseFoldedStr, int maxCharsOrNewlines, out bool foundString) {
        int strLength = caseFoldedStr.Length; // throws if str is null
        if (strLength == 0) throw new ArgumentException("The string argument is empty.");
        if (maxCharsOrNewlines < 0) throw new ArgumentOutOfRangeException("maxCharsOrNewlines is negative.");
        fixed (char* pstr = caseFoldedStr) {
            char* lineBegin = null;
            int nLines = 0;
            int nCRLF = 0;
            char first = pstr[0];
            char* ptr = Iter.Ptr;
            char* bufferEnd = Iter.Anchor->BufferEnd;
            char* end1 = unchecked (bufferEnd - strLength);
            if (end1 >= ptr && end1 < bufferEnd) {
                char* end2 = unchecked (ptr + maxCharsOrNewlines);
                char* end = end1 <= end2 || end2 < ptr ? end1 : end2;
                if (Iter.Block == Iter.Anchor->Block && ptr < end) {
                    char* cftable = CaseFoldTable.FoldedChars;
                    if (cftable == null) cftable = CaseFoldTable.Initialize();
                    for (;;) {
                        char c = cftable[*ptr];
                        if (c == first) goto CompareRestOfString;
                    StringsNotEqual:
                        ++ptr;
                        if (c > '\r') {
                            if (ptr >= end) break;
                        } else {
                            if (c == '\r') {
                                if (*ptr == '\n') {
                                    ++ptr;
                                    ++nCRLF;
                                    if (end < end1) ++end;
                                }
                            } else if (c != '\n') goto CheckBound;
                            lineBegin = ptr;
                            ++nLines;
                        CheckBound:
                            if (ptr >= end) break;
                        }
                        continue;
                    CompareRestOfString:
                        if (strLength == 1 || RestOfStringEqualsCI(ptr, pstr, strLength)) goto Found;
                        goto StringsNotEqual;
                    } // for
                    if (cftable[*ptr] == first && RestOfStringEqualsCI(ptr, pstr, strLength)) goto Found;
                    if (ptr >= end1) goto EndOfBlock;
                    foundString = false;
                    goto ReturnState;
                Found:
                    foundString = true;
                ReturnState:
                    if (ptr != Iter.Ptr) {
                        if (nLines == 0) return AdvanceTo(ptr);
                        else return AdvanceTo(ptr, lineBegin, nLines);
                    } else return this;
                }
            }
        EndOfBlock:
            return SkipToStringCIContinue(pstr, strLength, maxCharsOrNewlines, out foundString, ptr, lineBegin, nLines, nCRLF);
        }
    }
    private State<TUserState> SkipToStringCIContinue(char* pstr, int strLength, int maxCharsOrNewlines, out bool foundString,
                                                     char* ptr, char* lineBegin, int nLines, int nCRLF)
    {
        foundString = false;
        char first = *pstr;
        int index = CharStream.PositiveDistance(Iter.Ptr, ptr);
        int count = index - nCRLF;
        int lineBeginCount = nLines == 0 ? 0 : count - CharStream.PositiveDistance(lineBegin, ptr);
        char* cftable = CaseFoldTable.FoldedChars;
        if (cftable == null) cftable = CaseFoldTable.Initialize();
        CharStream.Iterator iter = Iter;
        char c = cftable[iter._Increment((uint)index)];
        for (;;) {
            if (c != first || !iter.MatchCaseFolded(pstr, strLength)) {
                if (c == CharStream.Iterator.EndOfStreamChar || count == maxCharsOrNewlines) break;
                ++count;
                char c1 = cftable[iter._Increment()];
                if (c > '\r') c = c1;
                else if (c == '\r') {
                    if (c1 == '\n') {
                        ++nCRLF;
                        c = cftable[iter._Increment()];
                    } else {
                        c = c1;
                    }
                    lineBeginCount = count;
                    ++nLines;
                } else if (c == '\n') {
                    c = c1;
                    lineBeginCount = count;
                    ++nLines;
                } else c = c1;
            } else {
                foundString = true;
                break;
            }
        }
        if (count != 0) {
            if (nLines == 0) return AdvanceTo(iter);
            else return AdvanceTo(iter, nLines, count - lineBeginCount);
        } else return this;
    }

    public State<TUserState> SkipToStringCI(string caseFoldedStr, int maxCharsOrNewlines, out string skippedString) {
        int strLength = caseFoldedStr.Length; // throws if str is null
        if (strLength == 0) throw new ArgumentException("The string argument is empty.");
        if (maxCharsOrNewlines < 0) throw new ArgumentOutOfRangeException("maxCharsOrNewlines is negative.");
        fixed (char* pstr = caseFoldedStr) {
            char* lineBegin = null;
            int nLines = 0;
            int nCRLF = 0;
            int nCR = 0;
            char first = pstr[0];
            char* ptr = Iter.Ptr;
            char* bufferEnd = Iter.Anchor->BufferEnd;
            char* end1 = unchecked (bufferEnd - strLength); // could overflow
            if (end1 >= ptr && end1 < bufferEnd) {
                char* end2 = unchecked (ptr + maxCharsOrNewlines); // could overflow
                char* end = end1 <= end2 || end2 < ptr ? end1 : end2;
                if (Iter.Block == Iter.Anchor->Block && ptr < end) {
                    char* cftable = CaseFoldTable.FoldedChars;
                    if (cftable == null) cftable = CaseFoldTable.Initialize();
                    for (;;) {
                        char c = cftable[*ptr];
                        if (c == first) goto CompareRestOfString;
                    StringsNotEqual:
                        ++ptr;
                        if (c > '\r') {
                            if (ptr >= end) break;
                        } else {
                            if (c == '\r') {
                                if (*ptr == '\n') {
                                    ++ptr;
                                    ++nCRLF;
                                    if (end < end1) ++end;
                                } else {
                                    ++nCR;
                                }
                            } else if (c != '\n') goto CheckBound;
                            lineBegin = ptr;
                            ++nLines;
                        CheckBound:
                            if (ptr >= end) break;
                        }
                        continue;
                    CompareRestOfString:
                        if (strLength == 1 || RestOfStringEqualsCI(ptr, pstr, strLength)) goto Found;
                        goto StringsNotEqual;
                    } // for
                    if (cftable[*ptr] == first && RestOfStringEqualsCI(ptr, pstr, strLength)) goto Found;
                    if (ptr >= end1) goto EndOfBlock;
                    goto NotFound;
                Found:
                    {
                        char* ptr0 = Iter.Ptr;
                        if (ptr != ptr0) {
                            int length = CharStream.PositiveDistance(ptr0, ptr);
                            if (nLines == 0) {
                                skippedString = new string(ptr0, 0, length);
                                return AdvanceTo(ptr);
                            } else {
                                skippedString = (nCR | nCRLF) == 0
                                                ? new string(ptr0, 0, length)
                                                : CharStream.CopyWithNormalizedNewlines(ptr0, length, nCRLF, nCR);
                                return AdvanceTo(ptr, lineBegin, nLines);
                            }
                        } else {
                            skippedString = "";
                            return this;
                        }
                    }
                NotFound:
                    {
                        skippedString = null;
                        char* ptr0 = Iter.Ptr;
                        if (ptr != ptr0) {
                            int length = CharStream.PositiveDistance(ptr0, ptr);
                            if (nLines == 0) return AdvanceTo(ptr);
                            else return AdvanceTo(ptr, lineBegin, nLines);
                        } else return this;
                    }
                }
            }
        EndOfBlock:
            return SkipToStringCIContinue(pstr, strLength, maxCharsOrNewlines, out skippedString, ptr, lineBegin, nLines, nCRLF, nCR);
        }
    }
    private State<TUserState> SkipToStringCIContinue(char* pstr, int strLength, int maxCharsOrNewlines, out string skippedString,
                                                     char* ptr, char* lineBegin, int nLines, int nCRLF, int nCR)
    {
        char first = *pstr;
        CharStream.Iterator iter = Iter;
        int index = CharStream.PositiveDistance(Iter.Ptr, ptr);
        int count = index - nCRLF;
        int lineBeginCount = nLines == 0 ? 0 : count - CharStream.PositiveDistance(lineBegin, ptr);
        char* cftable = CaseFoldTable.FoldedChars;
        if (cftable == null) cftable = CaseFoldTable.Initialize();
        char c = cftable[iter._Increment((uint)index)];
        for (;;) {
            if (c != first || !iter.MatchCaseFolded(pstr, strLength)) {
                if (c == CharStream.Iterator.EndOfStreamChar || count == maxCharsOrNewlines) break;
                ++count;
                char c1 = cftable[iter._Increment()];
                if (c > '\r') c = c1;
                else if (c == '\r') {
                    if (c1 == '\n') {
                        ++nCRLF;
                        c = cftable[iter._Increment()];
                    } else {
                        ++nCR;
                        c = c1;
                    }
                    lineBeginCount = count;
                    ++nLines;
                } else if (c == '\n') {
                    c = c1;
                    lineBeginCount = count;
                    ++nLines;
                } else c = c1;
            } else { // found string
                if (count != 0) {
                    if (nLines == 0) {
                        skippedString = Iter.ReadUntil(iter);
                        return AdvanceTo(iter);
                    } else {
                        string s = Iter.ReadUntil(iter);
                        if ((nCR | nCRLF) == 0) skippedString = s;
                        else {
                            fixed (char* ptr0 = s)
                                skippedString = CharStream.CopyWithNormalizedNewlines(ptr0, count + nCRLF, nCRLF, nCR);
                        }
                        return AdvanceTo(iter, nLines, count - lineBeginCount);
                    }
                } else {
                    skippedString = "";
                    return this;
                }
            }
        }
        skippedString = null;
        if (count != 0) {
            if (nLines == 0) return AdvanceTo(iter);
            else return AdvanceTo(iter, nLines, count - lineBeginCount);
        } else return this;
    }

} // class state

}

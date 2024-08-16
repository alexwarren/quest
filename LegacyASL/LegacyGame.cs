﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace TextAdventures.Quest.LegacyASL
{
    public class LegacyGame : IASL, IASLTimer
    {
        public enum State
        {
            Ready,       // game is not doing any processing, and is ready for a command
            Working,     // game is processing a command
            Waiting,     // while processing a command, game has encountered e.g. an "enter" script, and is awaiting further input
            Finished    // game is over
        }

        private class DefineBlock
        {
            public int StartLine;
            public int EndLine;
        }

        internal class Context
        {
            public int CallingObjectId;
            public int NumParameters;
            public string[] Parameters;
            public string FunctionReturnValue;
            public bool AllowRealNamesInCommand;
            public bool DontProcessCommand;
            public bool CancelExec;
            public int StackCounter;
        }

        private Context CopyContext(Context ctx)
        {
            var result = new Context();
            result.CallingObjectId = ctx.CallingObjectId;
            result.NumParameters = ctx.NumParameters;
            result.Parameters = ctx.Parameters;
            result.FunctionReturnValue = ctx.FunctionReturnValue;
            result.AllowRealNamesInCommand = ctx.AllowRealNamesInCommand;
            result.DontProcessCommand = ctx.DontProcessCommand;
            result.CancelExec = ctx.CancelExec;
            result.StackCounter = ctx.StackCounter;
            return result;
        }

        internal enum LogType
        {
            Misc,
            FatalError,
            WarningError,
            Init,
            LibraryWarningError,
            Warning,
            UserError,
            InternalError
        }

        private Dictionary<string, Dictionary<string, string>> _defineBlockParams;

        internal enum Direction
        {
            None = -1,
            Out = 0,
            North = 1,
            South = 2,
            East = 3,
            West = 4,
            NorthWest = 5,
            NorthEast = 6,
            SouthWest = 7,
            SouthEast = 8,
            Up = 9,
            Down = 10
        }

        private class ItemType
        {
            public string Name;
            public bool Got;
        }

        private class Collectable
        {
            public string Name;
            public string Type;
            public double Value;
            public string Display;
            public bool DisplayWhenZero;
        }

        internal class PropertyType
        {
            public string PropertyName;
            public string PropertyValue;
        }

        internal class ActionType
        {
            public string ActionName;
            public string Script;
        }

        internal class UseDataType
        {
            public string UseObject;
            public UseType UseType;
            public string UseScript;
        }

        internal class GiveDataType
        {
            public string GiveObject;
            public GiveType GiveType;
            public string GiveScript;
        }

        private class PropertiesActions
        {
            public string Properties;
            public int NumberActions;
            public ActionType[] Actions;
            public int NumberTypesIncluded;
            public string[] TypesIncluded;
        }

        private class VariableType
        {
            public string VariableName;
            public string[] VariableContents;
            public int VariableUBound;
            public string DisplayString;
            public string OnChangeScript;
            public bool NoZeroDisplay;
        }

        private class SynonymType
        {
            public string OriginalWord;
            public string ConvertTo;
        }

        private class TimerType
        {
            public string TimerName;
            public int TimerInterval;
            public bool TimerActive;
            public string TimerAction;
            public int TimerTicks;
            public bool BypassThisTurn;
        }

        internal class UserDefinedCommandType
        {
            public string CommandText;
            public string CommandScript;
        }

        internal class TextAction
        {
            public string Data;
            public TextActionType Type;
        }

        internal enum TextActionType
        {
            Text,
            Script,
            Nothing,
            Default
        }

        internal class ScriptText
        {
            public string Text;
            public string Script;
        }

        internal class PlaceType
        {
            public string PlaceName;
            public string Prefix;
            public string Script;
        }

        internal class RoomType
        {
            public string RoomName;
            public string RoomAlias;
            public UserDefinedCommandType[] Commands;
            public int NumberCommands;
            public TextAction Description = new TextAction();
            public ScriptText Out = new ScriptText();
            public TextAction East = new TextAction();
            public TextAction West = new TextAction();
            public TextAction North = new TextAction();
            public TextAction South = new TextAction();
            public TextAction NorthEast = new TextAction();
            public TextAction NorthWest = new TextAction();
            public TextAction SouthEast = new TextAction();
            public TextAction SouthWest = new TextAction();
            public TextAction Up = new TextAction();
            public TextAction Down = new TextAction();
            public string InDescription;
            public string Look;
            public PlaceType[] Places;
            public int NumberPlaces;
            public string Prefix;
            public string Script;
            public ScriptText[] Use;
            public int NumberUse;
            public int ObjId;
            public string BeforeTurnScript;
            public string AfterTurnScript;
            public LegacyASL.RoomExits Exits;
        }

        internal class ObjectType
        {
            public string ObjectName;
            public string ObjectAlias;
            public string Detail;
            public string ContainerRoom;
            public bool Exists;
            public string Prefix;
            public string Suffix;
            public string Gender;
            public string Article;
            public int DefinitionSectionStart;
            public int DefinitionSectionEnd;
            public bool Visible;
            public string GainScript;
            public string LoseScript;
            public int NumberProperties;
            public PropertyType[] Properties;
            public TextAction Speak = new TextAction();
            public TextAction Take = new TextAction();
            public bool IsRoom;
            public bool IsExit;
            public string CorresRoom;
            public int CorresRoomId;
            public bool Loaded;
            public int NumberActions;
            public ActionType[] Actions;
            public int NumberUseData;
            public UseDataType[] UseData;
            public string UseAnything;
            public string UseOnAnything;
            public string Use;
            public int NumberGiveData;
            public GiveDataType[] GiveData;
            public string GiveAnything;
            public string GiveToAnything;
            public string DisplayType;
            public int NumberTypesIncluded;
            public string[] TypesIncluded;
            public int NumberAltNames;
            public string[] AltNames;
            public TextAction AddScript = new TextAction();
            public TextAction RemoveScript = new TextAction();
            public TextAction OpenScript = new TextAction();
            public TextAction CloseScript = new TextAction();
        }

        private class ChangeType
        {
            public string AppliesTo;
            public string Change;
        }

        private class GameChangeDataType
        {
            public int NumberChanges;
            public ChangeType[] ChangeData;
        }

        private class ResourceType
        {
            public string ResourceName;
            public int ResourceStart;
            public int ResourceLength;
            public bool Extracted;
        }

        private class ExpressionResult
        {
            public string Result;
            public ExpressionSuccess Success;
            public string Message;
        }

        internal enum PlayerError
        {
            BadCommand,
            BadGo,
            BadGive,
            BadCharacter,
            NoItem,
            ItemUnwanted,
            BadLook,
            BadThing,
            DefaultLook,
            DefaultSpeak,
            BadItem,
            DefaultTake,
            BadUse,
            DefaultUse,
            DefaultOut,
            BadPlace,
            BadExamine,
            DefaultExamine,
            BadTake,
            CantDrop,
            DefaultDrop,
            BadDrop,
            BadPronoun,
            AlreadyOpen,
            AlreadyClosed,
            CantOpen,
            CantClose,
            DefaultOpen,
            DefaultClose,
            BadPut,
            CantPut,
            DefaultPut,
            CantRemove,
            AlreadyPut,
            DefaultRemove,
            Locked,
            DefaultWait,
            AlreadyTaken
        }

        private enum ItType
        {
            Inanimate,
            Male,
            Female
        }

        private enum SetResult
        {
            Error,
            Found,
            Unfound
        }

        private enum Thing
        {
            Character,
            Object,
            Room
        }

        private enum ConvertType
        {
            Strings,
            Functions,
            Numeric,
            Collectables
        }

        internal enum UseType
        {
            UseOnSomething,
            UseSomethingOn
        }

        internal enum GiveType
        {
            GiveToSomething,
            GiveSomethingTo
        }

        private enum VarType
        {
            String,
            Numeric
        }

        private enum StopType
        {
            Win,
            Lose,
            Null
        }

        private enum ExpressionSuccess
        {
            OK,
            Fail
        }

        private string _openErrorReport;
        private string[] _casKeywords = new string[256]; // Tokenised CAS keywords
        private string[] _lines; // Stores the lines of the ASL script/definitions
        private DefineBlock[] _defineBlocks; // Stores the start and end lines of each 'define' section
        private int _numberSections; // Number of define sections
        private string _gameName; // The name of the game
        internal Context _nullContext = new Context();
        private LegacyASL.ChangeLog _changeLogRooms;
        private LegacyASL.ChangeLog _changeLogObjects;
        private PropertiesActions _defaultProperties;
        private PropertiesActions _defaultRoomProperties;
        internal RoomType[] _rooms;
        internal int _numberRooms;
        private VariableType[] _numericVariable;
        private int _numberNumericVariables;
        private VariableType[] _stringVariable;
        private int _numberStringVariables;
        private SynonymType[] _synonyms;
        private int _numberSynonyms;
        private ItemType[] _items;
        private ObjectType[] _chars;
        internal ObjectType[] _objs;
        private int _numberChars;
        internal int _numberObjs;
        private int _numberItems;
        internal string _currentRoom;
        private Collectable[] _collectables;
        private int _numCollectables;
        private string _gamePath;
        private string _gameFileName;
        private string _saveGameFile;
        private string _defaultFontName;
        private double _defaultFontSize;
        private bool _autoIntro;
        private bool _commandOverrideModeOn;
        private string _commandOverrideVariable;
        private string _afterTurnScript;
        private string _beforeTurnScript;
        private bool _outPutOn;
        private int _gameAslVersion;
        private int _choiceNumber;
        private string _gameLoadMethod;
        private TimerType[] _timers;
        private int _numberTimers;
        private int _numDisplayStrings;
        private int _numDisplayNumerics;
        private bool _gameFullyLoaded;
        private GameChangeDataType _gameChangeData = new GameChangeDataType();
        private int _lastIt;
        private ItType _lastItMode;
        private int _thisTurnIt;
        private ItType _thisTurnItMode;
        private string _badCmdBefore;
        private string _badCmdAfter;
        private int _numResources;
        private ResourceType[] _resources;
        private string _resourceFile;
        private int _resourceOffset;
        private int _startCatPos;
        private bool _useAbbreviations;
        private bool _loadedFromQsg;
        private string _beforeSaveScript;
        private string _onLoadScript;
        private int _numSkipCheckFiles;
        private string[] _skipCheckFile;
        private List<ListData> _compassExits = new List<ListData>();
        private List<ListData> _gotoExits = new List<ListData>();
        private LegacyASL.TextFormatter _textFormatter = new LegacyASL.TextFormatter();
        private List<string> _log = new List<string>();
        private string _casFileData;
        private object _commandLock = new object();
        private object _stateLock = new object();
        private State _state = State.Ready;
        private object _waitLock = new object();
        private bool _readyForCommand = true;
        private bool _gameLoading;
        private Random _random = new Random();
        private string _tempFolder;
        private string[] _playerErrorMessageString = new string[39];
        private Dictionary<ListType, List<string>> _listVerbs = new Dictionary<ListType, List<string>>();
        private IGameData _gameData;
        private string _originalFilename;
        private IPlayer _player;
        private bool _gameFinished;
        private bool _gameIsRestoring;
        private bool _useStaticFrameForPictures;
        private string _fileData;
        private int _fileDataPos;
        private bool _questionResponse;

        public LegacyGame(IGameData gameData)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            _tempFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Quest", Guid.NewGuid().ToString());
            LoadCASKeywords();
            _gameLoadMethod = "normal";
            _gameData = gameData;
            _originalFilename = null;

            // Very early versions of Quest didn't perform very good syntax checking of ASL files, so this is
            // for compatibility with games which have non-fatal errors in them.
            _numSkipCheckFiles = 3;
            _skipCheckFile = new string[4];
            _skipCheckFile[1] = "bargain.cas";
            _skipCheckFile[2] = "easymoney.asl";
            _skipCheckFile[3] = "musicvf1.cas";
        }

        private string RemoveFormatting(string s)
        {
            string code;
            int pos, len = default;

            do
            {
                pos = Strings.InStr(s, "|");
                if (pos != 0)
                {
                    code = Strings.Mid(s, pos + 1, 3);

                    if (Strings.Left(code, 1) == "b")
                    {
                        len = 1;
                    }
                    else if (Strings.Left(code, 2) == "xb")
                    {
                        len = 2;
                    }
                    else if (Strings.Left(code, 1) == "u")
                    {
                        len = 1;
                    }
                    else if (Strings.Left(code, 2) == "xu")
                    {
                        len = 2;
                    }
                    else if (Strings.Left(code, 1) == "i")
                    {
                        len = 1;
                    }
                    else if (Strings.Left(code, 2) == "xi")
                    {
                        len = 2;
                    }
                    else if (Strings.Left(code, 2) == "cr")
                    {
                        len = 2;
                    }
                    else if (Strings.Left(code, 2) == "cb")
                    {
                        len = 2;
                    }
                    else if (Strings.Left(code, 2) == "cl")
                    {
                        len = 2;
                    }
                    else if (Strings.Left(code, 2) == "cy")
                    {
                        len = 2;
                    }
                    else if (Strings.Left(code, 2) == "cg")
                    {
                        len = 2;
                    }
                    else if (Strings.Left(code, 1) == "n")
                    {
                        len = 1;
                    }
                    else if (Strings.Left(code, 2) == "xn")
                    {
                        len = 2;
                    }
                    else if (Strings.Left(code, 1) == "s")
                    {
                        len = 3;
                    }
                    else if (Strings.Left(code, 2) == "jc")
                    {
                        len = 2;
                    }
                    else if (Strings.Left(code, 2) == "jl")
                    {
                        len = 2;
                    }
                    else if (Strings.Left(code, 2) == "jr")
                    {
                        len = 2;
                    }
                    else if (Strings.Left(code, 1) == "w")
                    {
                        len = 1;
                    }
                    else if (Strings.Left(code, 1) == "c")
                    {
                        len = 1;
                    }

                    if (len == 0)
                    {
                        // unknown code
                        len = 1;
                    }

                    s = Strings.Left(s, pos - 1) + Strings.Mid(s, pos + len + 1);
                }
            }

            while (pos != 0);

            return s;
        }

        private bool CheckSections()
        {
            int defines, braces;
            string checkLine = "";
            int bracePos;
            int pos;
            string section = "";
            bool hasErrors;
            var skipBlock = default(bool);
            _openErrorReport = "";
            hasErrors = false;
            defines = 0;
            braces = 0;

            for (int i = 1, loopTo = Information.UBound(_lines); i <= loopTo; i++)
            {
                if (!BeginsWith(_lines[i], "#!qdk-note: "))
                {
                    if (BeginsWith(_lines[i], "define "))
                    {
                        section = _lines[i];
                        braces = 0;
                        defines = defines + 1;
                        skipBlock = BeginsWith(_lines[i], "define text") | BeginsWith(_lines[i], "define synonyms");
                    }
                    else if (Strings.Trim(_lines[i]) == "end define")
                    {
                        defines = defines - 1;

                        if (defines < 0)
                        {
                            LogASLError("Extra 'end define' after block '" + section + "'", LogType.FatalError);
                            _openErrorReport = _openErrorReport + "Extra 'end define' after block '" + section + "'" + Constants.vbCrLf;
                            hasErrors = true;
                            defines = 0;
                        }

                        if (braces > 0)
                        {
                            LogASLError("Missing } in block '" + section + "'", LogType.FatalError);
                            _openErrorReport = _openErrorReport + "Missing } in block '" + section + "'" + Constants.vbCrLf;
                            hasErrors = true;
                        }
                        else if (braces < 0)
                        {
                            LogASLError("Too many } in block '" + section + "'", LogType.FatalError);
                            _openErrorReport = _openErrorReport + "Too many } in block '" + section + "'" + Constants.vbCrLf;
                            hasErrors = true;
                        }
                    }

                    if (Strings.Left(_lines[i], 1) != "'" & !skipBlock)
                    {
                        checkLine = ObliterateParameters(_lines[i]);
                        if (BeginsWith(checkLine, "'<ERROR;"))
                        {
                            // ObliterateParameters denotes a mismatched $, ( etc.
                            // by prefixing line with '<ERROR;*; where * is the mismatched
                            // character
                            LogASLError("Expected closing " + Strings.Mid(checkLine, 9, 1) + " character in '" + ReportErrorLine(_lines[i]) + "'", LogType.FatalError);
                            _openErrorReport = _openErrorReport + "Expected closing " + Strings.Mid(checkLine, 9, 1) + " character in '" + ReportErrorLine(_lines[i]) + "'." + Constants.vbCrLf;
                            return false;
                        }
                    }

                    if (Strings.Left(Strings.Trim(checkLine), 1) != "'")
                    {
                        // Now check {
                        pos = 1;
                        do
                        {
                            bracePos = Strings.InStr(pos, checkLine, "{");
                            if (bracePos != 0)
                            {
                                pos = bracePos + 1;
                                braces = braces + 1;
                            }
                        }
                        while (!(bracePos == 0 | pos > Strings.Len(checkLine)));

                        // Now check }
                        pos = 1;
                        do
                        {
                            bracePos = Strings.InStr(pos, checkLine, "}");
                            if (bracePos != 0)
                            {
                                pos = bracePos + 1;
                                braces = braces - 1;
                            }
                        }
                        while (!(bracePos == 0 | pos > Strings.Len(checkLine)));
                    }
                }
            }

            if (defines > 0)
            {
                LogASLError("Missing 'end define'", LogType.FatalError);
                _openErrorReport = _openErrorReport + "Missing 'end define'." + Constants.vbCrLf;
                hasErrors = true;
            }

            return !hasErrors;
        }

        private bool ConvertFriendlyIfs()
        {
            // Converts
            // if (%something% < 3) then ...
            // to
            // if is <%something%;lt;3> then ...
            // and also repeat until ...

            // Returns False if successful

            int convPos, symbPos;
            string symbol;
            int endParamPos;
            string paramData;
            int startParamPos;
            string firstData, secondData;
            string obscureLine, newParam, varObscureLine;
            int bracketCount;

            for (int i = 1, loopTo = Information.UBound(_lines); i <= loopTo; i++)
            {
                obscureLine = ObliterateParameters(_lines[i]);
                convPos = Strings.InStr(obscureLine, "if (");
                if (convPos == 0)
                {
                    convPos = Strings.InStr(obscureLine, "until (");
                }
                if (convPos == 0)
                {
                    convPos = Strings.InStr(obscureLine, "while (");
                }
                if (convPos == 0)
                {
                    convPos = Strings.InStr(obscureLine, "not (");
                }
                if (convPos == 0)
                {
                    convPos = Strings.InStr(obscureLine, "and (");
                }
                if (convPos == 0)
                {
                    convPos = Strings.InStr(obscureLine, "or (");
                }


                if (convPos != 0)
                {
                    varObscureLine = ObliterateVariableNames(_lines[i]);
                    if (BeginsWith(varObscureLine, "'<ERROR;"))
                    {
                        // ObliterateVariableNames denotes a mismatched #, % or $
                        // by prefixing line with '<ERROR;*; where * is the mismatched
                        // character
                        LogASLError("Expected closing " + Strings.Mid(varObscureLine, 9, 1) + " character in '" + ReportErrorLine(_lines[i]) + "'", LogType.FatalError);
                        return true;
                    }
                    startParamPos = Strings.InStr(convPos, _lines[i], "(");

                    endParamPos = 0;
                    bracketCount = 1;
                    for (int j = startParamPos + 1, loopTo1 = Strings.Len(_lines[i]); j <= loopTo1; j++)
                    {
                        if (Strings.Mid(_lines[i], j, 1) == "(")
                        {
                            bracketCount = bracketCount + 1;
                        }
                        else if (Strings.Mid(_lines[i], j, 1) == ")")
                        {
                            bracketCount = bracketCount - 1;
                        }
                        if (bracketCount == 0)
                        {
                            endParamPos = j;
                            break;
                        }
                    }

                    if (endParamPos == 0)
                    {
                        LogASLError("Expected ) in '" + ReportErrorLine(_lines[i]) + "'", LogType.FatalError);
                        return true;
                    }

                    paramData = Strings.Mid(_lines[i], startParamPos + 1, endParamPos - startParamPos - 1);

                    symbPos = Strings.InStr(paramData, "!=");
                    if (symbPos == 0)
                    {
                        symbPos = Strings.InStr(paramData, "<>");
                        if (symbPos == 0)
                        {
                            symbPos = Strings.InStr(paramData, "<=");
                            if (symbPos == 0)
                            {
                                symbPos = Strings.InStr(paramData, ">=");
                                if (symbPos == 0)
                                {
                                    symbPos = Strings.InStr(paramData, "<");
                                    if (symbPos == 0)
                                    {
                                        symbPos = Strings.InStr(paramData, ">");
                                        if (symbPos == 0)
                                        {
                                            symbPos = Strings.InStr(paramData, "=");
                                            if (symbPos == 0)
                                            {
                                                LogASLError("Unrecognised 'if' condition in '" + ReportErrorLine(_lines[i]) + "'", LogType.FatalError);
                                                return true;
                                            }
                                            else
                                            {
                                                symbol = "=";
                                            }
                                        }
                                        else
                                        {
                                            symbol = ">";
                                        }
                                    }
                                    else
                                    {
                                        symbol = "<";
                                    }
                                }
                                else
                                {
                                    symbol = ">=";
                                }
                            }
                            else
                            {
                                symbol = "<=";
                            }
                        }
                        else
                        {
                            symbol = "<>";
                        }
                    }
                    else
                    {
                        symbol = "<>";
                    }


                    firstData = Strings.Trim(Strings.Left(paramData, symbPos - 1));
                    secondData = Strings.Trim(Strings.Mid(paramData, symbPos + Strings.Len(symbol)));

                    if (symbol == "=")
                    {
                        newParam = "is <" + firstData + ";" + secondData + ">";
                    }
                    else
                    {
                        newParam = "is <" + firstData + ";";
                        if (symbol == "<")
                        {
                            newParam = newParam + "lt";
                        }
                        else if (symbol == ">")
                        {
                            newParam = newParam + "gt";
                        }
                        else if (symbol == ">=")
                        {
                            newParam = newParam + "gt=";
                        }
                        else if (symbol == "<=")
                        {
                            newParam = newParam + "lt=";
                        }
                        else if (symbol == "<>")
                        {
                            newParam = newParam + "!=";
                        }
                        newParam = newParam + ";" + secondData + ">";
                    }

                    _lines[i] = Strings.Left(_lines[i], startParamPos - 1) + newParam + Strings.Mid(_lines[i], endParamPos + 1);

                    // Repeat processing this line, in case there are
                    // further changes to be made.
                    i = i - 1;
                }
            }

            return false;
        }

        private void ConvertMultiLineSections()
        {
            int startLine, braceCount;
            string thisLine, lineToAdd;
            var lastBrace = default(int);
            int i;
            string restOfLine, procName;
            int endLineNum;
            string afterLastBrace, z;
            string startOfOrig;
            string testLine;
            int testBraceCount;
            int obp, cbp;
            var curProc = default(int);

            i = 1;
            do
            {
                z = _lines[_defineBlocks[i].StartLine];
                if (!BeginsWith(z, "define text ") & !BeginsWith(z, "define menu ") & z != "define synonyms")
                {
                    for (int j = _defineBlocks[i].StartLine + 1, loopTo = _defineBlocks[i].EndLine - 1; j <= loopTo; j++)
                    {
                        if (Strings.InStr(_lines[j], "{") > 0)
                        {

                            afterLastBrace = "";
                            thisLine = Strings.Trim(_lines[j]);

                            procName = "<!intproc" + curProc + ">";

                            // see if this brace's corresponding closing
                            // brace is on same line:

                            testLine = Strings.Mid(_lines[j], Strings.InStr(_lines[j], "{") + 1);
                            testBraceCount = 1;
                            do
                            {
                                obp = Strings.InStr(testLine, "{");
                                cbp = Strings.InStr(testLine, "}");
                                if (obp == 0)
                                    obp = Strings.Len(testLine) + 1;
                                if (cbp == 0)
                                    cbp = Strings.Len(testLine) + 1;
                                if (obp < cbp)
                                {
                                    testBraceCount = testBraceCount + 1;
                                    testLine = Strings.Mid(testLine, obp + 1);
                                }
                                else if (cbp < obp)
                                {
                                    testBraceCount = testBraceCount - 1;
                                    testLine = Strings.Mid(testLine, cbp + 1);
                                }
                            }
                            while (!(obp == cbp | testBraceCount == 0));

                            if (testBraceCount != 0)
                            {
                                AddLine("define procedure " + procName);
                                startLine = Information.UBound(_lines);
                                restOfLine = Strings.Trim(Strings.Right(thisLine, Strings.Len(thisLine) - Strings.InStr(thisLine, "{")));
                                braceCount = 1;
                                if (!string.IsNullOrEmpty(restOfLine))
                                    AddLine(restOfLine);

                                for (int m = 1, loopTo1 = Strings.Len(restOfLine); m <= loopTo1; m++)
                                {
                                    if (Strings.Mid(restOfLine, m, 1) == "{")
                                    {
                                        braceCount = braceCount + 1;
                                    }
                                    else if (Strings.Mid(restOfLine, m, 1) == "}")
                                    {
                                        braceCount = braceCount - 1;
                                    }
                                }

                                if (braceCount != 0)
                                {
                                    int k = j + 1;
                                    do
                                    {
                                        for (int m = 1, loopTo2 = Strings.Len(_lines[k]); m <= loopTo2; m++)
                                        {
                                            if (Strings.Mid(_lines[k], m, 1) == "{")
                                            {
                                                braceCount = braceCount + 1;
                                            }
                                            else if (Strings.Mid(_lines[k], m, 1) == "}")
                                            {
                                                braceCount = braceCount - 1;
                                            }

                                            if (braceCount == 0)
                                            {
                                                lastBrace = m;
                                                break;
                                            }
                                        }

                                        if (braceCount != 0)
                                        {
                                            // put Lines(k) into another variable, as
                                            // AddLine ReDims Lines, which it can't do if
                                            // passed Lines(x) as a parameter.
                                            lineToAdd = _lines[k];
                                            AddLine(lineToAdd);
                                        }
                                        else
                                        {
                                            AddLine(Strings.Left(_lines[k], lastBrace - 1));
                                            afterLastBrace = Strings.Trim(Strings.Mid(_lines[k], lastBrace + 1));
                                        }

                                        // Clear original line
                                        _lines[k] = "";
                                        k = k + 1;
                                    }
                                    while (braceCount != 0);
                                }

                                AddLine("end define");
                                endLineNum = Information.UBound(_lines);

                                _numberSections = _numberSections + 1;
                                Array.Resize(ref _defineBlocks, _numberSections + 1);
                                _defineBlocks[_numberSections] = new DefineBlock();
                                _defineBlocks[_numberSections].StartLine = startLine;
                                _defineBlocks[_numberSections].EndLine = endLineNum;

                                // Change original line where the { section
                                // started to call the new procedure.
                                startOfOrig = Strings.Trim(Strings.Left(thisLine, Strings.InStr(thisLine, "{") - 1));
                                _lines[j] = startOfOrig + " do " + procName + " " + afterLastBrace;
                                curProc = curProc + 1;

                                // Process this line again in case there was stuff after the last brace that included
                                // more braces. e.g. } else {
                                j = j - 1;
                            }
                        }
                    }
                }
                i = i + 1;
            }
            while (i <= _numberSections);

            // Join next-line "else"s to corresponding "if"s

            var loopTo3 = _numberSections;
            for (i = 1; i <= loopTo3; i++)
            {
                z = _lines[_defineBlocks[i].StartLine];
                if (!BeginsWith(z, "define text ") & !BeginsWith(z, "define menu ") & z != "define synonyms")
                {
                    for (int j = _defineBlocks[i].StartLine + 1, loopTo4 = _defineBlocks[i].EndLine - 1; j <= loopTo4; j++)
                    {
                        if (BeginsWith(_lines[j], "else "))
                        {

                            // Go upwards to find "if" statement that this
                            // belongs to

                            for (int k = j, loopTo5 = _defineBlocks[i].StartLine + 1; k >= loopTo5; k -= 1)
                            {
                                if (BeginsWith(_lines[k], "if ") | Strings.InStr(ObliterateParameters(_lines[k]), " if ") != 0)
                                {
                                    _lines[k] = _lines[k] + " " + Strings.Trim(_lines[j]);
                                    _lines[j] = "";
                                    k = _defineBlocks[i].StartLine;
                                }
                            }
                        }
                    }
                }
            }


        }

        private bool ErrorCheck()
        {
            // Parses ASL script for errors. Returns TRUE if OK;
            // False if a critical error is encountered.
            int curBegin, curEnd;
            bool hasErrors;
            int curPos;
            int numParamStart, numParamEnd;
            bool finLoop, inText;

            hasErrors = false;
            inText = false;

            // Checks for incorrect number of < and > :
            for (int i = 1, loopTo = Information.UBound(_lines); i <= loopTo; i++)
            {
                numParamStart = 0;
                numParamEnd = 0;

                if (BeginsWith(_lines[i], "define text "))
                {
                    inText = true;
                }
                if (inText & Strings.Trim(Strings.LCase(_lines[i])) == "end define")
                {
                    inText = false;
                }

                if (!inText)
                {
                    // Find number of <'s:
                    curPos = 1;
                    finLoop = false;
                    do
                    {
                        if (Strings.InStr(curPos, _lines[i], "<") != 0)
                        {
                            numParamStart = numParamStart + 1;
                            curPos = Strings.InStr(curPos, _lines[i], "<") + 1;
                        }
                        else
                        {
                            finLoop = true;
                        }
                    }
                    while (!finLoop);

                    // Find number of >'s:
                    curPos = 1;
                    finLoop = false;
                    do
                    {
                        if (Strings.InStr(curPos, _lines[i], ">") != 0)
                        {
                            numParamEnd = numParamEnd + 1;
                            curPos = Strings.InStr(curPos, _lines[i], ">") + 1;
                        }
                        else
                        {
                            finLoop = true;
                        }
                    }
                    while (!finLoop);

                    if (numParamStart > numParamEnd)
                    {
                        LogASLError("Expected > in " + ReportErrorLine(_lines[i]), LogType.FatalError);
                        hasErrors = true;
                    }
                    else if (numParamStart < numParamEnd)
                    {
                        LogASLError("Too many > in " + ReportErrorLine(_lines[i]), LogType.FatalError);
                        hasErrors = true;
                    }
                }
            }

            // Exit if errors found
            if (hasErrors == true)
            {
                return true;
            }

            // Checks that define sections have parameters:
            for (int i = 1, loopTo1 = _numberSections; i <= loopTo1; i++)
            {
                curBegin = _defineBlocks[i].StartLine;
                curEnd = _defineBlocks[i].EndLine;

                if (BeginsWith(_lines[curBegin], "define game"))
                {
                    if (Strings.InStr(_lines[curBegin], "<") == 0)
                    {
                        LogASLError("'define game' has no parameter - game has no name", LogType.FatalError);
                        return true;
                    }
                }
                else if (!BeginsWith(_lines[curBegin], "define synonyms") & !BeginsWith(_lines[curBegin], "define options"))
                {
                    if (Strings.InStr(_lines[curBegin], "<") == 0)
                    {
                        LogASLError(_lines[curBegin] + " has no parameter", LogType.FatalError);
                        hasErrors = true;
                    }
                }
            }

            return hasErrors;
        }

        private string GetAfterParameter(string s)
        {
            // Returns everything after the end of the first parameter
            // in a string, i.e. for "use <thing> do <myproc>" it
            // returns "do <myproc>"
            int eop;
            eop = Strings.InStr(s, ">");

            if (eop == 0 | eop + 1 > Strings.Len(s))
            {
                return "";
            }
            else
            {
                return Strings.Trim(Strings.Mid(s, eop + 1));
            }

        }

        private string ObliterateParameters(string s)
        {

            bool inParameter;
            string exitCharacter = "";
            string curChar;
            string outputLine = "";
            var obscuringFunctionName = default(bool);

            inParameter = false;

            for (int i = 1, loopTo = Strings.Len(s); i <= loopTo; i++)
            {
                curChar = Strings.Mid(s, i, 1);

                if (inParameter)
                {
                    if (exitCharacter == ")")
                    {
                        if (Strings.InStr("$#%", curChar) > 0)
                        {
                            // We might be converting a line like:
                            // if ( $rand(1;10)$ < 3 ) then {
                            // and we don't want it to end up like this:
                            // if (~~~~~~~~~~~)$ <~~~~~~~~~~~
                            // which will cause all sorts of confustion. So,
                            // we get rid of everything between the $ characters
                            // in this case, and set a flag so we know what we're
                            // doing.

                            obscuringFunctionName = true;
                            exitCharacter = curChar;

                            // Move along please

                            outputLine = outputLine + "~";
                            i = i + 1;
                            curChar = Strings.Mid(s, i, 1);
                        }
                    }
                }

                if (!inParameter)
                {
                    outputLine = outputLine + curChar;
                    if (curChar == "<")
                    {
                        inParameter = true;
                        exitCharacter = ">";
                    }
                    if (curChar == "(")
                    {
                        inParameter = true;
                        exitCharacter = ")";
                    }
                }
                else if ((curChar ?? "") == (exitCharacter ?? ""))
                {
                    if (!obscuringFunctionName)
                    {
                        inParameter = false;
                        outputLine = outputLine + curChar;
                    }
                    else
                    {
                        // We've finished obscuring the function name,
                        // now let's find the next ) as we were before
                        // we found this dastardly function
                        obscuringFunctionName = false;
                        exitCharacter = ")";
                        outputLine = outputLine + "~";
                    }
                }
                else
                {
                    outputLine = outputLine + "~";
                }
            }

            if (inParameter)
            {
                return "'<ERROR;" + exitCharacter + ";" + outputLine;
            }
            else
            {
                return outputLine;
            }

        }

        private string ObliterateVariableNames(string s)
        {
            bool inParameter;
            string exitCharacter = "";
            string outputLine = "";
            string curChar;

            inParameter = false;

            for (int i = 1, loopTo = Strings.Len(s); i <= loopTo; i++)
            {
                curChar = Strings.Mid(s, i, 1);
                if (!inParameter)
                {
                    outputLine = outputLine + curChar;
                    if (curChar == "$")
                    {
                        inParameter = true;
                        exitCharacter = "$";
                    }
                    if (curChar == "#")
                    {
                        inParameter = true;
                        exitCharacter = "#";
                    }
                    if (curChar == "%")
                    {
                        inParameter = true;
                        exitCharacter = "%";
                    }
                    // The ~ was for collectables, and this syntax only
                    // exists in Quest 2.x. The ~ was only finally
                    // allowed to be present on its own in ASL 320.
                    if (curChar == "~" & _gameAslVersion < 320)
                    {
                        inParameter = true;
                        exitCharacter = "~";
                    }
                }
                else if ((curChar ?? "") == (exitCharacter ?? ""))
                {
                    inParameter = false;
                    outputLine = outputLine + curChar;
                }
                else
                {
                    outputLine = outputLine + "X";
                }
            }

            if (inParameter)
            {
                outputLine = "'<ERROR;" + exitCharacter + ";" + outputLine;
            }

            return outputLine;

        }

        private void RemoveComments()
        {
            int aposPos;
            var inTextBlock = default(bool);
            var inSynonymsBlock = default(bool);
            string oblitLine;

            // If in a synonyms block, we want to remove lines which are comments, but
            // we don't want to remove synonyms that contain apostrophes, so we only
            // get rid of lines with an "'" at the beginning or with " '" in them

            for (int i = 1, loopTo = Information.UBound(_lines); i <= loopTo; i++)
            {

                if (BeginsWith(_lines[i], "'!qdk-note:"))
                {
                    _lines[i] = "#!qdk-note:" + GetEverythingAfter(_lines[i], "'!qdk-note:");
                }
                else
                {
                    if (BeginsWith(_lines[i], "define text "))
                    {
                        inTextBlock = true;
                    }
                    else if (Strings.Trim(_lines[i]) == "define synonyms")
                    {
                        inSynonymsBlock = true;
                    }
                    else if (BeginsWith(_lines[i], "define type "))
                    {
                        inSynonymsBlock = true;
                    }
                    else if (Strings.Trim(_lines[i]) == "end define")
                    {
                        inTextBlock = false;
                        inSynonymsBlock = false;
                    }

                    if (!inTextBlock & !inSynonymsBlock)
                    {
                        if (Strings.InStr(_lines[i], "'") > 0)
                        {
                            oblitLine = ObliterateParameters(_lines[i]);
                            if (!BeginsWith(oblitLine, "'<ERROR;"))
                            {
                                aposPos = Strings.InStr(oblitLine, "'");

                                if (aposPos != 0)
                                {
                                    _lines[i] = Strings.Trim(Strings.Left(_lines[i], aposPos - 1));
                                }
                            }
                        }
                    }
                    else if (inSynonymsBlock)
                    {
                        if (Strings.Left(Strings.Trim(_lines[i]), 1) == "'")
                        {
                            _lines[i] = "";
                        }
                        else
                        {
                            // we look for " '", not "'" in synonyms lines
                            aposPos = Strings.InStr(ObliterateParameters(_lines[i]), " '");
                            if (aposPos != 0)
                            {
                                _lines[i] = Strings.Trim(Strings.Left(_lines[i], aposPos - 1));
                            }
                        }
                    }
                }

            }
        }

        private string ReportErrorLine(string s)
        {
            // We don't want to see the "!intproc" in logged error reports lines.
            // This function replaces these "do" lines with a nicer-looking "..." for error reporting.

            int replaceFrom;

            replaceFrom = Strings.InStr(s, "do <!intproc");
            if (replaceFrom != 0)
            {
                return Strings.Left(s, replaceFrom - 1) + "...";
            }
            else
            {
                return s;
            }
        }

        private string YesNo(bool yn)
        {
            if (yn == true)
                return "Yes";
            else
                return "No";
        }

        private bool IsYes(string yn)
        {
            if (Strings.LCase(yn) == "yes")
                return true;
            else
                return false;
        }

        internal bool BeginsWith(string s, string text)
        {
            // Compares the beginning of the line with a given
            // string. Case insensitive.

            // Example: beginswith("Hello there","HeLlO")=TRUE

            return (Strings.Left(Strings.LTrim(Strings.LCase(s)), Strings.Len(text)) ?? "") == (Strings.LCase(text) ?? "");
        }

        private string ConvertCasKeyword(string casChar)
        {
            byte c = System.Text.Encoding.GetEncoding(1252).GetBytes(casChar)[0];
            string keyword = _casKeywords[c];

            if (keyword == "!cr")
            {
                keyword = Constants.vbCrLf;
            }

            return keyword;
        }

        private void ConvertMultiLines()
        {
            // Goes through each section capable of containing
            // script commands and puts any multiple-line script commands
            // into separate procedures. Also joins multiple-line "if"
            // statements.

            // This calls RemoveComments after joining lines, so that lines
            // with "'" as part of a multi-line parameter are not destroyed,
            // before looking for braces.

            for (int i = Information.UBound(_lines); i >= 1; i -= 1)
            {
                if (Strings.Right(_lines[i], 2) == "__")
                {
                    _lines[i] = Strings.Left(_lines[i], Strings.Len(_lines[i]) - 2) + Strings.LTrim(_lines[i + 1]);
                    _lines[i + 1] = "";
                    // Recalculate this line again
                    i = i + 1;
                }
                else if (Strings.Right(_lines[i], 1) == "_")
                {
                    _lines[i] = Strings.Left(_lines[i], Strings.Len(_lines[i]) - 1) + Strings.LTrim(_lines[i + 1]);
                    _lines[i + 1] = "";
                    // Recalculate this line again
                    i = i + 1;
                }
            }

            RemoveComments();
        }

        private DefineBlock GetDefineBlock(string blockname)
        {

            // Returns the start and end points of a named block.
            // Returns 0 if block not found.

            string l, blockType;

            var result = new DefineBlock();
            result.StartLine = 0;
            result.EndLine = 0;

            for (int i = 1, loopTo = _numberSections; i <= loopTo; i++)
            {
                // Get the first line of the define section:
                l = _lines[_defineBlocks[i].StartLine];

                // Now, starting from the first word after 'define',
                // retrieve the next word and compare it to blockname:

                // Add a space for define blocks with no parameter
                if (Strings.InStr(8, l, " ") == 0)
                    l = l + " ";

                blockType = Strings.Mid(l, 8, Strings.InStr(8, l, " ") - 8);

                if ((blockType ?? "") == (blockname ?? ""))
                {
                    // Return the start and end points
                    result.StartLine = _defineBlocks[i].StartLine;
                    result.EndLine = _defineBlocks[i].EndLine;
                    return result;
                }
            }

            return result;
        }

        private DefineBlock DefineBlockParam(string blockname, string @param)
        {
            // Returns the start and end points of a named block

            Dictionary<string, string> cache;
            var result = new DefineBlock();

            @param = "k" + @param; // protect against numeric block names

            if (!_defineBlockParams.ContainsKey(blockname))
            {
                // Lazily create cache of define block params

                cache = new Dictionary<string, string>();
                _defineBlockParams.Add(blockname, cache);

                for (int i = 1, loopTo = _numberSections; i <= loopTo; i++)
                {
                    // get the word after "define", e.g. "procedure"
                    string blockType = GetEverythingAfter(_lines[_defineBlocks[i].StartLine], "define ");
                    int sp = Strings.InStr(blockType, " ");
                    if (sp != 0)
                    {
                        blockType = Strings.Trim(Strings.Left(blockType, sp - 1));
                    }

                    if ((blockType ?? "") == (blockname ?? ""))
                    {
                        string blockKey = GetParameter(_lines[_defineBlocks[i].StartLine], _nullContext, false);

                        blockKey = "k" + blockKey;

                        if (!cache.ContainsKey(blockKey))
                        {
                            cache.Add(blockKey, _defineBlocks[i].StartLine + "," + _defineBlocks[i].EndLine);
                        }
                        else
                        {
                            // silently ignore duplicates
                        }
                    }
                }
            }
            else
            {
                cache = _defineBlockParams[blockname];
            }

            if (cache.ContainsKey(@param))
            {
                string[] blocks = Strings.Split(cache[@param], ",");
                result.StartLine = Conversions.ToInteger(blocks[0]);
                result.EndLine = Conversions.ToInteger(blocks[1]);
            }

            return result;

        }

        internal string GetEverythingAfter(string s, string text)
        {
            if (Strings.Len(text) > Strings.Len(s))
            {
                return "";
            }
            return Strings.Right(s, Strings.Len(s) - Strings.Len(text));
        }

        private string Keyword2CAS(string KWord)
        {
            if (string.IsNullOrEmpty(KWord))
            {
                return "";
            }

            for (int i = 0; i <= 255; i++)
            {
                if ((Strings.LCase(KWord) ?? "") == (Strings.LCase(_casKeywords[i]) ?? ""))
                {
                    return Conversions.ToString(Strings.Chr(i));
                }
            }

            return Keyword2CAS("!unknown") + KWord + Keyword2CAS("!unknown");
        }

        private void LoadCASKeywords()
        {
            // Loads data required for conversion of CAS files

            string[] questDatLines = this.GetResourceLines(Resources.GetResourceBytes(Resources.QuestDAT));

            foreach (var line in questDatLines)
            {
                if (Strings.Left(line, 1) != "#")
                {
                    // Lines isn't a comment - so parse it.
                    int scp = Strings.InStr(line, ";");
                    string keyword = Strings.Trim(Strings.Left(line, scp - 1));
                    int num = Conversions.ToInteger(Strings.Right(line, Strings.Len(line) - scp));
                    _casKeywords[num] = keyword;
                }
            }
        }

        private string[] GetResourceLines(byte[] res)
        {
            var enc = new System.Text.UTF8Encoding();
            string resFile = enc.GetString(res);
            return Strings.Split(resFile, "\r" + "\n");
        }

        private async Task<string> GetFileData(IGameData gameData)
        {
            var stream = gameData.Data;
            return await new System.IO.StreamReader(stream).ReadToEndAsync();
        }

        private async Task<bool> ParseFile(IGameData gameData)
        {
            // Returns FALSE if failed.

            bool hasErrors;
            bool result;
            string[] libCode = new string[1];
            int libLines;
            bool ignoreMode, skipCheck;
            int c, d, l;
            int libFileHandle;
            string[] libResourceLines;
            string libFile;
            string libLine;
            int inDefGameBlock, gameLine = default;
            int inDefSynBlock, synLine = default;
            bool libFoundThisSweep;
            string libFileName;
            string[] libraryList = new string[1];
            int numLibraries;
            bool libraryAlreadyIncluded;
            int inDefTypeBlock;
            string typeBlockName;
            var typeLine = default(int);
            int defineCount, curLine;
            string filename = gameData.Filename;

            _defineBlockParams = new Dictionary<string, Dictionary<string, string>>();

            result = true;

            // Parses file and returns the positions of each main
            // 'define' block. Supports nested defines.

            if (Strings.LCase(Strings.Right(filename, 4)) == ".zip")
            {
                _originalFilename = filename;
                filename = GetUnzippedFile(filename);
                _gamePath = System.IO.Path.GetDirectoryName(filename);
            }

            if (Strings.LCase(Strings.Right(filename, 4)) == ".asl" | Strings.LCase(Strings.Right(filename, 4)) == ".txt")
            {
                // Read file into Lines array
                string fileData = await GetFileData(gameData);

                string[] aslLines = fileData.Split('\r');
                _lines = new string[aslLines.Length + 1];
                _lines[0] = "";

                var loopTo = aslLines.Length;
                for (l = 1; l <= loopTo; l++)
                {
                    _lines[l] = aslLines[l - 1];
                    _lines[l] = RemoveTabs(_lines[l]);
                    _lines[l] = _lines[l].Trim(' ', '\n', '\r');
                }

                l = aslLines.Length;
            }

            else if (Strings.LCase(Strings.Right(filename, 4)) == ".cas")
            {
                LogASLError("Loading CAS");
                LoadCASFile(filename);
                l = Information.UBound(_lines);
            }

            else
            {
                throw new InvalidOperationException("Unrecognized file extension");
            }

            // Add libraries to end of code:

            numLibraries = 0;

            do
            {
                libFoundThisSweep = false;
                for (int i = l; i >= 1; i -= 1)
                {
                    // We search for includes backwards as a game might include
                    // some-general.lib and then something-specific.lib which needs
                    // something-general; if we include something-specific first,
                    // then something-general afterwards, something-general's startscript
                    // gets executed before something-specific's, as we execute the
                    // lib startscripts backwards as well
                    if (BeginsWith(_lines[i], "!include "))
                    {
                        libFileName = GetParameter(_lines[i], _nullContext);
                        // Clear !include statement
                        _lines[i] = "";
                        libraryAlreadyIncluded = false;
                        LogASLError("Including library '" + libFileName + "'...", LogType.Init);

                        for (int j = 1, loopTo1 = numLibraries; j <= loopTo1; j++)
                        {
                            if ((Strings.LCase(libFileName) ?? "") == (Strings.LCase(libraryList[j]) ?? ""))
                            {
                                libraryAlreadyIncluded = true;
                                break;
                            }
                        }

                        if (libraryAlreadyIncluded)
                        {
                            LogASLError("     - Library already included.", LogType.Init);
                        }
                        else
                        {
                            numLibraries = numLibraries + 1;
                            Array.Resize(ref libraryList, numLibraries + 1);
                            libraryList[numLibraries] = libFileName;

                            libFoundThisSweep = true;
                            libResourceLines = null;

                            libFile = _gamePath + libFileName;
                            LogASLError(" - Searching for " + libFile + " (game path)", LogType.Init);
                            libFileHandle = FileSystem.FreeFile();

                            if (System.IO.File.Exists(libFile))
                            {
                                FileSystem.FileOpen(libFileHandle, libFile, OpenMode.Input);
                            }
                            else
                            {
                                // File was not found; try standard Quest libraries (stored here as resources)

                                LogASLError("     - Library not found in game path.", LogType.Init);
                                LogASLError(" - Searching for " + libFile + " (standard libraries)", LogType.Init);
                                libResourceLines = GetLibraryLines(libFileName);

                                if (libResourceLines is null)
                                {
                                    LogASLError("Library not found.", LogType.FatalError);
                                    _openErrorReport = _openErrorReport + "Library '" + libraryList[numLibraries] + "' not found." + Constants.vbCrLf;
                                    return false;
                                }
                            }

                            LogASLError("     - Found library, opening...", LogType.Init);

                            libLines = 0;

                            if (libResourceLines is null)
                            {
                                do
                                {
                                    libLines = libLines + 1;
                                    libLine = FileSystem.LineInput(libFileHandle);
                                    libLine = RemoveTabs(libLine);
                                    Array.Resize(ref libCode, libLines + 1);
                                    libCode[libLines] = Strings.Trim(libLine);
                                }
                                while (!FileSystem.EOF(libFileHandle));
                                FileSystem.FileClose(libFileHandle);
                            }
                            else
                            {
                                foreach (string resLibLine in libResourceLines)
                                {
                                    libLines = libLines + 1;
                                    Array.Resize(ref libCode, libLines + 1);
                                    libLine = resLibLine;
                                    libLine = RemoveTabs(libLine);
                                    libCode[libLines] = Strings.Trim(libLine);
                                }
                            }

                            int libVer = -1;

                            if (libCode[1] == "!library")
                            {
                                var loopTo2 = libLines;
                                for (c = 1; c <= loopTo2; c++)
                                {
                                    if (BeginsWith(libCode[c], "!asl-version "))
                                    {
                                        libVer = Conversions.ToInteger(GetParameter(libCode[c], _nullContext));
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                // Old library
                                libVer = 100;
                            }

                            if (libVer == -1)
                            {
                                LogASLError(" - Library has no asl-version information.", LogType.LibraryWarningError);
                                libVer = 200;
                            }

                            ignoreMode = false;
                            var loopTo3 = libLines;
                            for (c = 1; c <= loopTo3; c++)
                            {
                                if (BeginsWith(libCode[c], "!include "))
                                {
                                    // Quest only honours !include in a library for asl-version
                                    // 311 or later, as it ignored them in versions < 3.11
                                    if (libVer >= 311)
                                    {
                                        AddLine(libCode[c]);
                                        l = l + 1;
                                    }
                                }
                                else if (Strings.Left(libCode[c], 1) != "!" & Strings.Left(libCode[c], 1) != "'" & !ignoreMode)
                                {
                                    AddLine(libCode[c]);
                                    l = l + 1;
                                }
                                else if (libCode[c] == "!addto game")
                                {
                                    inDefGameBlock = 0;
                                    var loopTo4 = Information.UBound(_lines);
                                    for (d = 1; d <= loopTo4; d++)
                                    {
                                        if (BeginsWith(_lines[d], "define game "))
                                        {
                                            inDefGameBlock = 1;
                                        }
                                        else if (BeginsWith(_lines[d], "define "))
                                        {
                                            if (inDefGameBlock != 0)
                                            {
                                                inDefGameBlock = inDefGameBlock + 1;
                                            }
                                        }
                                        else if (_lines[d] == "end define" & inDefGameBlock == 1)
                                        {
                                            gameLine = d;
                                            d = Information.UBound(_lines);
                                        }
                                        else if (_lines[d] == "end define")
                                        {
                                            if (inDefGameBlock != 0)
                                            {
                                                inDefGameBlock = inDefGameBlock - 1;
                                            }
                                        }
                                    }

                                    do
                                    {
                                        c = c + 1;
                                        if (!BeginsWith(libCode[c], "!end"))
                                        {
                                            Array.Resize(ref _lines, Information.UBound(_lines) + 1 + 1);
                                            var loopTo5 = gameLine + 1;
                                            for (d = Information.UBound(_lines); d >= loopTo5; d -= 1)
                                                _lines[d] = _lines[d - 1];

                                            // startscript lines in a library are prepended
                                            // with "lib" internally so they are executed
                                            // before any startscript specified by the
                                            // calling ASL file, for asl-versions 311 and
                                            // later.

                                            // similarly, commands in a library. NB: without this, lib
                                            // verbs have lower precedence than game verbs anyway. Also
                                            // lib commands have lower precedence than game commands. We
                                            // only need this code so that game verbs have a higher
                                            // precedence than lib commands.

                                            // we also need it so that lib verbs have a higher
                                            // precedence than lib commands.

                                            if (libVer >= 311 & BeginsWith(libCode[c], "startscript "))
                                            {
                                                _lines[gameLine] = "lib " + libCode[c];
                                            }
                                            else if (libVer >= 392 & (BeginsWith(libCode[c], "command ") | BeginsWith(libCode[c], "verb ")))
                                            {
                                                _lines[gameLine] = "lib " + libCode[c];
                                            }
                                            else
                                            {
                                                _lines[gameLine] = libCode[c];
                                            }

                                            l = l + 1;
                                            gameLine = gameLine + 1;
                                        }
                                    }
                                    while (!BeginsWith(libCode[c], "!end"));
                                }
                                else if (libCode[c] == "!addto synonyms")
                                {
                                    inDefSynBlock = 0;
                                    var loopTo6 = Information.UBound(_lines);
                                    for (d = 1; d <= loopTo6; d++)
                                    {
                                        if (_lines[d] == "define synonyms")
                                        {
                                            inDefSynBlock = 1;
                                        }
                                        else if (_lines[d] == "end define" & inDefSynBlock == 1)
                                        {
                                            synLine = d;
                                            d = Information.UBound(_lines);
                                        }
                                    }

                                    if (inDefSynBlock == 0)
                                    {
                                        // No "define synonyms" block in game - so add it
                                        AddLine("define synonyms");
                                        AddLine("end define");
                                        synLine = Information.UBound(_lines);
                                    }

                                    do
                                    {
                                        c = c + 1;
                                        if (!BeginsWith(libCode[c], "!end"))
                                        {
                                            Array.Resize(ref _lines, Information.UBound(_lines) + 1 + 1);
                                            var loopTo7 = synLine + 1;
                                            for (d = Information.UBound(_lines); d >= loopTo7; d -= 1)
                                                _lines[d] = _lines[d - 1];
                                            _lines[synLine] = libCode[c];
                                            l = l + 1;
                                            synLine = synLine + 1;
                                        }
                                    }
                                    while (!BeginsWith(libCode[c], "!end"));
                                }
                                else if (BeginsWith(libCode[c], "!addto type "))
                                {
                                    inDefTypeBlock = 0;
                                    typeBlockName = Strings.LCase(GetParameter(libCode[c], _nullContext));
                                    var loopTo8 = Information.UBound(_lines);
                                    for (d = 1; d <= loopTo8; d++)
                                    {
                                        if ((Strings.LCase(_lines[d]) ?? "") == ("define type <" + typeBlockName + ">" ?? ""))
                                        {
                                            inDefTypeBlock = 1;
                                        }
                                        else if (_lines[d] == "end define" & inDefTypeBlock == 1)
                                        {
                                            typeLine = d;
                                            d = Information.UBound(_lines);
                                        }
                                    }

                                    if (inDefTypeBlock == 0)
                                    {
                                        // No "define type (whatever)" block in game - so add it
                                        AddLine("define type <" + typeBlockName + ">");
                                        AddLine("end define");
                                        typeLine = Information.UBound(_lines);
                                    }

                                    do
                                    {
                                        c = c + 1;
                                        if (c > libLines)
                                            break;
                                        if (!BeginsWith(libCode[c], "!end"))
                                        {
                                            Array.Resize(ref _lines, Information.UBound(_lines) + 1 + 1);
                                            var loopTo9 = typeLine + 1;
                                            for (d = Information.UBound(_lines); d >= loopTo9; d -= 1)
                                                _lines[d] = _lines[d - 1];
                                            _lines[typeLine] = libCode[c];
                                            l = l + 1;
                                            typeLine = typeLine + 1;
                                        }
                                    }
                                    while (!BeginsWith(libCode[c], "!end"));
                                }


                                else if (libCode[c] == "!library")
                                {
                                }
                                // ignore
                                else if (BeginsWith(libCode[c], "!asl-version "))
                                {
                                }
                                // ignore
                                else if (BeginsWith(libCode[c], "'"))
                                {
                                }
                                // ignore
                                else if (BeginsWith(libCode[c], "!QDK"))
                                {
                                    ignoreMode = true;
                                }
                                else if (BeginsWith(libCode[c], "!end"))
                                {
                                    ignoreMode = false;
                                }
                            }
                        }
                    }
                }
            }
            while (libFoundThisSweep != false);

            skipCheck = false;

            int lastSlashPos = default, slashPos;
            int curPos = 1;
            do
            {
                slashPos = Strings.InStr(curPos, filename, @"\");
                if (slashPos == 0)
                    slashPos = Strings.InStr(curPos, filename, "/");
                if (slashPos != 0)
                    lastSlashPos = slashPos;
                curPos = slashPos + 1;
            }
            while (slashPos != 0);
            string filenameNoPath = Strings.LCase(Strings.Mid(filename, lastSlashPos + 1));

            for (int i = 1, loopTo10 = _numSkipCheckFiles; i <= loopTo10; i++)
            {
                if ((filenameNoPath ?? "") == (_skipCheckFile[i] ?? ""))
                {
                    skipCheck = true;
                    break;
                }
            }

            if (filenameNoPath == "musicvf1.cas")
            {
                _useStaticFrameForPictures = true;
            }

            // RemoveComments called within ConvertMultiLines
            ConvertMultiLines();

            if (!skipCheck)
            {
                if (!CheckSections())
                {
                    return false;
                }
            }

            _numberSections = 1;

            for (int i = 1, loopTo11 = l; i <= loopTo11; i++)
            {
                // find section beginning with 'define'
                if (BeginsWith(_lines[i], "define") == true)
                {
                    // Now, go through until we reach an 'end define'. However, if we
                    // encounter another 'define' there is a nested define. So, if we
                    // encounter 'define' we increment the definecount. When we find an
                    // 'end define' we decrement it. When definecount is zero, we have
                    // found the end of the section.

                    defineCount = 1;

                    // Don't count the current line - we know it begins with 'define'...
                    curLine = i + 1;
                    do
                    {
                        if (BeginsWith(_lines[curLine], "define") == true)
                        {
                            defineCount = defineCount + 1;
                        }
                        else if (BeginsWith(_lines[curLine], "end define") == true)
                        {
                            defineCount = defineCount - 1;
                        }
                        curLine = curLine + 1;
                    }
                    while (defineCount != 0);
                    curLine = curLine - 1;

                    // Now, we know that the define section begins at i and ends at
                    // curline. Remember where the section begins and ends:

                    Array.Resize(ref _defineBlocks, _numberSections + 1);
                    _defineBlocks[_numberSections] = new DefineBlock();
                    _defineBlocks[_numberSections].StartLine = i;
                    _defineBlocks[_numberSections].EndLine = curLine;

                    _numberSections = _numberSections + 1;
                    i = curLine;
                }
            }

            _numberSections = _numberSections - 1;

            bool gotGameBlock = false;
            for (int i = 1, loopTo12 = _numberSections; i <= loopTo12; i++)
            {
                if (BeginsWith(_lines[_defineBlocks[i].StartLine], "define game "))
                {
                    gotGameBlock = true;
                    break;
                }
            }

            if (!gotGameBlock)
            {
                _openErrorReport = _openErrorReport + "No 'define game' block." + Constants.vbCrLf;
                return false;
            }

            ConvertMultiLineSections();

            hasErrors = ConvertFriendlyIfs();
            if (!hasErrors)
                hasErrors = ErrorCheck();

            if (hasErrors)
            {
                throw new InvalidOperationException("Errors found in game file.");
            }

            _saveGameFile = "";

            return result;
        }

        internal void LogASLError(string err, LogType @type = LogType.Misc)
        {
            if (type == LogType.FatalError)
            {
                err = "FATAL ERROR: " + err;
            }
            else if (type == LogType.WarningError)
            {
                err = "ERROR: " + err;
            }
            else if (type == LogType.LibraryWarningError)
            {
                err = "WARNING ERROR (LIBRARY): " + err;
            }
            else if (type == LogType.Init)
            {
                err = "INIT: " + err;
            }
            else if (type == LogType.Warning)
            {
                err = "WARNING: " + err;
            }
            else if (type == LogType.UserError)
            {
                err = "ERROR (REQUESTED): " + err;
            }
            else if (type == LogType.InternalError)
            {
                err = "INTERNAL ERROR: " + err;
            }

            _log.Add(err);
            _player.Log(err);
        }

        internal string GetParameter(string s, Context ctx, bool convertStringVariables = true)
        {
            // Returns the parameters between < and > in a string
            string newParam;
            int startPos;
            int endPos;

            startPos = Strings.InStr(s, "<");
            endPos = Strings.InStr(s, ">");

            if (startPos == 0 | endPos == 0)
            {
                LogASLError("Expected parameter in '" + ReportErrorLine(s) + "'", LogType.WarningError);
                return "";
            }

            string retrParam = Strings.Mid(s, startPos + 1, endPos - startPos - 1);

            if (convertStringVariables)
            {
                if (_gameAslVersion >= 320)
                {
                    newParam = ConvertParameter(ConvertParameter(ConvertParameter(retrParam, "#", ConvertType.Strings, ctx), "%", ConvertType.Numeric, ctx), "$", ConvertType.Functions, ctx);
                }
                else if (!(Strings.Left(retrParam, 9) == "~Internal"))
                {
                    newParam = ConvertParameter(ConvertParameter(ConvertParameter(ConvertParameter(retrParam, "#", ConvertType.Strings, ctx), "%", ConvertType.Numeric, ctx), "~", ConvertType.Collectables, ctx), "$", ConvertType.Functions, ctx);
                }
                else
                {
                    newParam = retrParam;
                }
            }
            else
            {
                newParam = retrParam;
            }

            return EvaluateInlineExpressions(newParam);
        }

        private void AddLine(string line)
        {
            // Adds a line to the game script
            int numLines;

            numLines = Information.UBound(_lines) + 1;
            Array.Resize(ref _lines, numLines + 1);
            _lines[numLines] = line;
        }

        private string GetCASFileData(string filename)
        {
            return System.IO.File.ReadAllText(filename, System.Text.Encoding.GetEncoding(1252));
        }

        private void LoadCASFile(string filename)
        {
            bool endLineReached, exitTheLoop;
            var textMode = default(bool);
            int casVersion;
            string startCat = "";
            int endCatPos;
            string chkVer;
            var j = default(int);
            string curLin, textData;
            int cpos, nextLinePos;
            string c, tl, ckw, d;

            _lines = new string[1];

            string fileData = GetCASFileData(filename);

            chkVer = Strings.Left(fileData, 7);
            if (chkVer == "QCGF001")
            {
                casVersion = 1;
            }
            else if (chkVer == "QCGF002")
            {
                casVersion = 2;
            }
            else if (chkVer == "QCGF003")
            {
                casVersion = 3;
            }
            else
            {
                throw new InvalidOperationException("Invalid or corrupted CAS file.");
            }

            if (casVersion == 3)
            {
                startCat = Keyword2CAS("!startcat");
            }

            for (int i = 9, loopTo = Strings.Len(fileData); i <= loopTo; i++)
            {
                if (casVersion == 3 & (Strings.Mid(fileData, i, 1) ?? "") == (startCat ?? ""))
                {
                    // Read catalog
                    _startCatPos = i;
                    endCatPos = Strings.InStr(j, fileData, Keyword2CAS("!endcat"));
                    ReadCatalog(Strings.Mid(fileData, j + 1, endCatPos - j - 1));
                    _resourceFile = filename;
                    _resourceOffset = endCatPos + 1;
                    i = Strings.Len(fileData);
                    _casFileData = fileData;
                }
                else
                {

                    curLin = "";
                    endLineReached = false;
                    if (textMode == true)
                    {
                        textData = Strings.Mid(fileData, i, Strings.InStr(i, fileData, Conversions.ToString(Strings.Chr(253))) - (i - 1));
                        textData = Strings.Left(textData, Strings.Len(textData) - 1);
                        cpos = 1;
                        bool finished = false;

                        if (!string.IsNullOrEmpty(textData))
                        {

                            do
                            {
                                nextLinePos = Strings.InStr(cpos, textData, "\0");
                                if (nextLinePos == 0)
                                {
                                    nextLinePos = Strings.Len(textData) + 1;
                                    finished = true;
                                }
                                tl = DecryptString(Strings.Mid(textData, cpos, nextLinePos - cpos));
                                AddLine(tl);
                                cpos = nextLinePos + 1;
                            }
                            while (!finished);

                        }

                        textMode = false;
                        i = Strings.InStr(i, fileData, Conversions.ToString(Strings.Chr(253)));
                    }

                    j = i;
                    do
                    {
                        ckw = Strings.Mid(fileData, j, 1);
                        c = ConvertCasKeyword(ckw);

                        if ((c ?? "") == Constants.vbCrLf)
                        {
                            endLineReached = true;
                        }
                        else if (Strings.Left(c, 1) != "!")
                        {
                            curLin = curLin + c + " ";
                        }
                        else if (c == "!quote")
                        {
                            exitTheLoop = false;
                            curLin = curLin + "<";
                            do
                            {
                                j = j + 1;
                                d = Strings.Mid(fileData, j, 1);
                                if (d != "\0")
                                {
                                    curLin = curLin + DecryptString(d);
                                }
                                else
                                {
                                    curLin = curLin + "> ";
                                    exitTheLoop = true;
                                }
                            }
                            while (!exitTheLoop);
                        }
                        else if (c == "!unknown")
                        {
                            exitTheLoop = false;
                            do
                            {
                                j = j + 1;
                                d = Strings.Mid(fileData, j, 1);
                                if (d != Conversions.ToString(Strings.Chr(254)))
                                {
                                    curLin = curLin + d;
                                }
                                else
                                {
                                    exitTheLoop = true;
                                }
                            }
                            while (!exitTheLoop);
                            curLin = curLin + " ";
                        }

                        j = j + 1;
                    }
                    while (!endLineReached);
                    AddLine(Strings.Trim(curLin));
                    if (BeginsWith(curLin, "define text") | casVersion >= 2 & (BeginsWith(curLin, "define synonyms") | BeginsWith(curLin, "define type") | BeginsWith(curLin, "define menu")))
                    {
                        textMode = true;
                    }
                    // j is already at correct place, but i will be
                    // incremented - so put j back one or we will miss a
                    // character.
                    i = j - 1;
                }
            }
        }

        private string DecryptString(string s)
        {
            string output = "";
            for (int i = 1, loopTo = Strings.Len(s); i <= loopTo; i++)
            {
                byte v = System.Text.Encoding.GetEncoding(1252).GetBytes(Strings.Mid(s, i, 1))[0];
                output = output + Strings.Chr(v ^ 255);
            }

            return output;
        }

        private string RemoveTabs(string s)
        {
            if (Strings.InStr(s, "\t") > 0)
            {
                // Remove tab characters and change them into
                // spaces; otherwise they bugger up the Trim
                // commands.
                int cpos = 1;
                bool finished = false;
                do
                {
                    int tabChar = Strings.InStr(cpos, s, "\t");
                    if (tabChar != 0)
                    {
                        s = Strings.Left(s, tabChar - 1) + Strings.Space(4) + Strings.Mid(s, tabChar + 1);
                        cpos = tabChar + 1;
                    }
                    else
                    {
                        finished = true;
                    }
                }
                while (!finished);
            }

            return s;
        }

        private void DoAddRemove(int childId, int parentId, bool @add, Context ctx)
        {
            if (@add)
            {
                AddToObjectProperties("parent=" + _objs[parentId].ObjectName, childId, ctx);
                _objs[childId].ContainerRoom = _objs[parentId].ContainerRoom;
            }
            else
            {
                AddToObjectProperties("not parent", childId, ctx);
            }

            if (_gameAslVersion >= 410)
            {
                // Putting something in a container implicitly makes that
                // container "seen". Otherwise we could try to "look at" the
                // object we just put in the container and have disambigution fail!
                AddToObjectProperties("seen", parentId, ctx);
            }

            UpdateVisibilityInContainers(ctx, _objs[parentId].ObjectName);
        }

        private void DoLook(int id, Context ctx, bool showExamineError = false, bool showDefaultDescription = true)
        {
            string objectContents;
            bool foundLook = false;

            // First, set the "seen" property, and for ASL >= 391, update visibility for any
            // object that is contained by this object.

            if (_gameAslVersion >= 391)
            {
                AddToObjectProperties("seen", id, ctx);
                UpdateVisibilityInContainers(ctx, _objs[id].ObjectName);
            }

            // First look for action, then look
            // for property, then check define
            // section:

            string lookLine;
            var o = _objs[id];

            for (int i = 1, loopTo = o.NumberActions; i <= loopTo; i++)
            {
                if (o.Actions[i].ActionName == "look")
                {
                    foundLook = true;
                    ExecuteScript(o.Actions[i].Script, ctx);
                    break;
                }
            }

            if (!foundLook)
            {
                for (int i = 1, loopTo1 = o.NumberProperties; i <= loopTo1; i++)
                {
                    if (o.Properties[i].PropertyName == "look")
                    {
                        // do this odd RetrieveParameter stuff to convert any variables
                        Print(GetParameter("<" + o.Properties[i].PropertyValue + ">", ctx), ctx);
                        foundLook = true;
                        break;
                    }
                }
            }

            if (!foundLook)
            {
                for (int i = o.DefinitionSectionStart, loopTo2 = o.DefinitionSectionEnd; i <= loopTo2; i++)
                {
                    if (BeginsWith(_lines[i], "look "))
                    {

                        lookLine = Strings.Trim(GetEverythingAfter(_lines[i], "look "));

                        if (Strings.Left(lookLine, 1) == "<")
                        {
                            Print(GetParameter(_lines[i], ctx), ctx);
                        }
                        else
                        {
                            ExecuteScript(lookLine, ctx, id);
                        }

                        foundLook = true;
                    }
                }
            }

            if (_gameAslVersion >= 391)
            {
                objectContents = ListContents(id, ctx);
            }
            else
            {
                objectContents = "";
            }

            if (!foundLook & showDefaultDescription)
            {
                PlayerError err;

                if (showExamineError)
                {
                    err = PlayerError.DefaultExamine;
                }
                else
                {
                    err = PlayerError.DefaultLook;
                }

                // Print "Nothing out of the ordinary" or whatever, but only if we're not going to list
                // any contents.

                if (string.IsNullOrEmpty(objectContents))
                    PlayerErrorMessage(err, ctx);
            }

            if (!string.IsNullOrEmpty(objectContents) & objectContents != "<script>")
                Print(objectContents, ctx);

        }

        private void DoOpenClose(int id, bool open, bool showLook, Context ctx)
        {
            if (open)
            {
                AddToObjectProperties("opened", id, ctx);
                if (showLook)
                    DoLook(id, ctx, showDefaultDescription: false);
            }
            else
            {
                AddToObjectProperties("not opened", id, ctx);
            }

            UpdateVisibilityInContainers(ctx, _objs[id].ObjectName);
        }

        private string EvaluateInlineExpressions(string s)
        {
            // Evaluates in-line expressions e.g. msg <Hello, did you know that 2 + 2 = {2+2}?>

            if (_gameAslVersion < 391)
            {
                return s;
            }

            int bracePos;
            int curPos = 1;
            string resultLine = "";

            do
            {
                bracePos = Strings.InStr(curPos, s, "{");

                if (bracePos != 0)
                {

                    resultLine = resultLine + Strings.Mid(s, curPos, bracePos - curPos);

                    if (Strings.Mid(s, bracePos, 2) == "{{")
                    {
                        // {{ = {
                        curPos = bracePos + 2;
                        resultLine = resultLine + "{";
                    }
                    else
                    {
                        int EndBracePos = Strings.InStr(bracePos + 1, s, "}");
                        if (EndBracePos == 0)
                        {
                            LogASLError("Expected } in '" + s + "'", LogType.WarningError);
                            return "<ERROR>";
                        }
                        else
                        {
                            string expression = Strings.Mid(s, bracePos + 1, EndBracePos - bracePos - 1);
                            var expResult = ExpressionHandler(expression);
                            if (expResult.Success != ExpressionSuccess.OK)
                            {
                                LogASLError("Error evaluating expression in <" + s + "> - " + expResult.Message);
                                return "<ERROR>";
                            }

                            resultLine = resultLine + expResult.Result;
                            curPos = EndBracePos + 1;
                        }
                    }
                }
                else
                {
                    resultLine = resultLine + Strings.Mid(s, curPos);
                }
            }
            while (!(bracePos == 0 | curPos > Strings.Len(s)));

            // Above, we only bothered checking for {{. But for consistency, also }} = }. So let's do that:
            curPos = 1;
            do
            {
                bracePos = Strings.InStr(curPos, resultLine, "}}");
                if (bracePos != 0)
                {
                    resultLine = Strings.Left(resultLine, bracePos) + Strings.Mid(resultLine, bracePos + 2);
                    curPos = bracePos + 1;
                }
            }
            while (!(bracePos == 0 | curPos > Strings.Len(resultLine)));

            return resultLine;
        }

        private void ExecAddRemove(string cmd, Context ctx)
        {
            int childId;
            string childName;
            var doAdd = default(bool);
            int sepPos = default, parentId, sepLen = default;
            string parentName;
            string verb = "";
            string action;
            var foundAction = default(bool);
            string actionScript = "";
            bool propertyExists;
            string textToPrint;
            bool isContainer;
            bool gotObject;
            int childLength;
            bool noParentSpecified = false;

            if (BeginsWith(cmd, "put "))
            {
                verb = "put";
                doAdd = true;
                sepPos = Strings.InStr(cmd, " on ");
                sepLen = 4;
                if (sepPos == 0)
                {
                    sepPos = Strings.InStr(cmd, " in ");
                    sepLen = 4;
                }
                if (sepPos == 0)
                {
                    sepPos = Strings.InStr(cmd, " onto ");
                    sepLen = 6;
                }
            }
            else if (BeginsWith(cmd, "add "))
            {
                verb = "add";
                doAdd = true;
                sepPos = Strings.InStr(cmd, " to ");
                sepLen = 4;
            }
            else if (BeginsWith(cmd, "remove "))
            {
                verb = "remove";
                doAdd = false;
                sepPos = Strings.InStr(cmd, " from ");
                sepLen = 6;
            }

            if (sepPos == 0)
            {
                noParentSpecified = true;
                sepPos = Strings.Len(cmd) + 1;
            }

            childLength = sepPos - (Strings.Len(verb) + 2);

            if (childLength < 0)
            {
                PlayerErrorMessage(PlayerError.BadCommand, ctx);
                _badCmdBefore = verb;
                return;
            }

            childName = Strings.Trim(Strings.Mid(cmd, Strings.Len(verb) + 2, childLength));

            gotObject = false;

            if (_gameAslVersion >= 392 & doAdd)
            {
                childId = Disambiguate(childName, _currentRoom + ";inventory", ctx);

                if (childId > 0)
                {
                    if (_objs[childId].ContainerRoom == "inventory")
                    {
                        gotObject = true;
                    }
                    else
                    {
                        // Player is not carrying the object they referred to. So, first take the object.
                        Print("(first taking " + _objs[childId].Article + ")", ctx);
                        // Try to take the object
                        ctx.AllowRealNamesInCommand = true;
                        ExecCommand("take " + _objs[childId].ObjectName, ctx, false, dontSetIt: true);

                        if (_objs[childId].ContainerRoom == "inventory")
                            gotObject = true;
                    }

                    if (!gotObject)
                    {
                        _badCmdBefore = verb;
                        return;
                    }
                }
                else
                {
                    if (childId != -2)
                        PlayerErrorMessage(PlayerError.NoItem, ctx);
                    _badCmdBefore = verb;
                    return;
                }
            }

            else
            {
                childId = Disambiguate(childName, "inventory;" + _currentRoom, ctx);

                if (childId <= 0)
                {
                    if (childId != -2)
                        PlayerErrorMessage(PlayerError.BadThing, ctx);
                    _badCmdBefore = verb;
                    return;
                }
            }

            if (noParentSpecified & doAdd)
            {
                SetStringContents("quest.error.article", _objs[childId].Article, ctx);
                PlayerErrorMessage(PlayerError.BadPut, ctx);
                return;
            }

            if (doAdd)
            {
                action = "add";
            }
            else
            {
                action = "remove";
            }

            if (!noParentSpecified)
            {
                parentName = Strings.Trim(Strings.Mid(cmd, sepPos + sepLen));

                parentId = Disambiguate(parentName, _currentRoom + ";inventory", ctx);

                if (parentId <= 0)
                {
                    if (parentId != -2)
                        PlayerErrorMessage(PlayerError.BadThing, ctx);
                    _badCmdBefore = Strings.Left(cmd, sepPos + sepLen);
                    return;
                }
            }
            else
            {
                // Assume the player was referring to the parent that the object is already in,
                // if it is even in an object already

                if (!IsYes(GetObjectProperty("parent", childId, true, false)))
                {
                    PlayerErrorMessage(PlayerError.CantRemove, ctx);
                    return;
                }

                parentId = GetObjectIdNoAlias(GetObjectProperty("parent", childId, false, true));
            }

            // Check if parent is a container

            isContainer = IsYes(GetObjectProperty("container", parentId, true, false));

            if (!isContainer)
            {
                if (doAdd)
                {
                    PlayerErrorMessage(PlayerError.CantPut, ctx);
                }
                else
                {
                    PlayerErrorMessage(PlayerError.CantRemove, ctx);
                }
                return;
            }

            // Check object is already held by that parent

            if (IsYes(GetObjectProperty("parent", childId, true, false)))
            {
                if (doAdd & (Strings.LCase(GetObjectProperty("parent", childId, false, false)) ?? "") == (Strings.LCase(_objs[parentId].ObjectName) ?? ""))
                {
                    PlayerErrorMessage(PlayerError.AlreadyPut, ctx);
                }
            }

            // Check parent and child are accessible to player
            var canAccessObject = PlayerCanAccessObject(childId);
            if (!canAccessObject.CanAccessObject)
            {
                if (doAdd)
                {
                    PlayerErrorMessage_ExtendInfo(PlayerError.CantPut, ctx, canAccessObject.ErrorMsg);
                }
                else
                {
                    PlayerErrorMessage_ExtendInfo(PlayerError.CantRemove, ctx, canAccessObject.ErrorMsg);
                }
                return;
            }

            var canAccessParent = PlayerCanAccessObject(parentId);
            if (!canAccessParent.CanAccessObject)
            {
                if (doAdd)
                {
                    PlayerErrorMessage_ExtendInfo(PlayerError.CantPut, ctx, canAccessParent.ErrorMsg);
                }
                else
                {
                    PlayerErrorMessage_ExtendInfo(PlayerError.CantRemove, ctx, canAccessParent.ErrorMsg);
                }
                return;
            }

            // Check if parent is a closed container

            if (!IsYes(GetObjectProperty("surface", parentId, true, false)) & !IsYes(GetObjectProperty("opened", parentId, true, false)))
            {
                // Not a surface and not open, so can't add to this closed container.
                if (doAdd)
                {
                    PlayerErrorMessage(PlayerError.CantPut, ctx);
                }
                else
                {
                    PlayerErrorMessage(PlayerError.CantRemove, ctx);
                }
                return;
            }

            // Now check if it can be added to (or removed from)

            // First check for an action
            var o = _objs[parentId];
            for (int i = 1, loopTo = o.NumberActions; i <= loopTo; i++)
            {
                if ((Strings.LCase(o.Actions[i].ActionName) ?? "") == (action ?? ""))
                {
                    foundAction = true;
                    actionScript = o.Actions[i].Script;
                    break;
                }
            }

            if (foundAction)
            {
                SetStringContents("quest." + Strings.LCase(action) + ".object.name", _objs[childId].ObjectName, ctx);
                ExecuteScript(actionScript, ctx, parentId);
            }
            else
            {
                // Now check for a property
                propertyExists = IsYes(GetObjectProperty(action, parentId, true, false));

                if (!propertyExists)
                {
                    // Show error message
                    if (doAdd)
                    {
                        PlayerErrorMessage(PlayerError.CantPut, ctx);
                    }
                    else
                    {
                        PlayerErrorMessage(PlayerError.CantRemove, ctx);
                    }
                }
                else
                {
                    textToPrint = GetObjectProperty(action, parentId, false, false);
                    if (string.IsNullOrEmpty(textToPrint))
                    {
                        // Show default message
                        if (doAdd)
                        {
                            PlayerErrorMessage(PlayerError.DefaultPut, ctx);
                        }
                        else
                        {
                            PlayerErrorMessage(PlayerError.DefaultRemove, ctx);
                        }
                    }
                    else
                    {
                        Print(textToPrint, ctx);
                    }

                    DoAddRemove(childId, parentId, doAdd, ctx);

                }
            }

        }

        private void ExecAddRemoveScript(string parameter, bool @add, Context ctx)
        {
            int childId, parentId = default;
            string commandName;
            string childName;
            string parentName = "";
            int scp;

            if (@add)
            {
                commandName = "add";
            }
            else
            {
                commandName = "remove";
            }

            scp = Strings.InStr(parameter, ";");
            if (scp == 0 & @add)
            {
                LogASLError("No parent specified in '" + commandName + " <" + parameter + ">", LogType.WarningError);
                return;
            }

            if (scp != 0)
            {
                childName = Strings.LCase(Strings.Trim(Strings.Left(parameter, scp - 1)));
                parentName = Strings.LCase(Strings.Trim(Strings.Mid(parameter, scp + 1)));
            }
            else
            {
                childName = Strings.LCase(Strings.Trim(parameter));
            }

            childId = GetObjectIdNoAlias(childName);
            if (childId == 0)
            {
                LogASLError("Invalid child object name specified in '" + commandName + " <" + parameter + ">", LogType.WarningError);
                return;
            }

            if (scp != 0)
            {
                parentId = GetObjectIdNoAlias(parentName);
                if (parentId == 0)
                {
                    LogASLError("Invalid parent object name specified in '" + commandName + " <" + parameter + ">", LogType.WarningError);
                    return;
                }

                DoAddRemove(childId, parentId, @add, ctx);
            }
            else
            {
                AddToObjectProperties("not parent", childId, ctx);
                UpdateVisibilityInContainers(ctx, _objs[parentId].ObjectName);
            }
        }

        private void ExecOpenClose(string cmd, Context ctx)
        {
            int id;
            string name;
            var doOpen = default(bool);
            bool isOpen, foundAction = default;
            string action = "";
            string actionScript = "";
            bool propertyExists;
            string textToPrint;
            bool isContainer;

            if (BeginsWith(cmd, "open "))
            {
                action = "open";
                doOpen = true;
            }
            else if (BeginsWith(cmd, "close "))
            {
                action = "close";
                doOpen = false;
            }

            name = GetEverythingAfter(cmd, action + " ");

            id = Disambiguate(name, _currentRoom + ";inventory", ctx);

            if (id <= 0)
            {
                if (id != -2)
                    PlayerErrorMessage(PlayerError.BadThing, ctx);
                _badCmdBefore = action;
                return;
            }

            // Check if it's even a container

            isContainer = IsYes(GetObjectProperty("container", id, true, false));

            if (!isContainer)
            {
                if (doOpen)
                {
                    PlayerErrorMessage(PlayerError.CantOpen, ctx);
                }
                else
                {
                    PlayerErrorMessage(PlayerError.CantClose, ctx);
                }
                return;
            }

            // Check if it's already open (or closed)

            isOpen = IsYes(GetObjectProperty("opened", id, true, false));

            if (doOpen & isOpen)
            {
                // Object is already open
                PlayerErrorMessage(PlayerError.AlreadyOpen, ctx);
                return;
            }
            else if (!doOpen & !isOpen)
            {
                // Object is already closed
                PlayerErrorMessage(PlayerError.AlreadyClosed, ctx);
                return;
            }

            // Check if it's accessible, i.e. check it's not itself inside another closed container

            var canAccessObject = PlayerCanAccessObject(id);
            if (!canAccessObject.CanAccessObject)
            {
                if (doOpen)
                {
                    PlayerErrorMessage_ExtendInfo(PlayerError.CantOpen, ctx, canAccessObject.ErrorMsg);
                }
                else
                {
                    PlayerErrorMessage_ExtendInfo(PlayerError.CantClose, ctx, canAccessObject.ErrorMsg);
                }
                return;
            }

            // Now check if it can be opened (or closed)

            // First check for an action
            var o = _objs[id];
            for (int i = 1, loopTo = o.NumberActions; i <= loopTo; i++)
            {
                if ((Strings.LCase(o.Actions[i].ActionName) ?? "") == (action ?? ""))
                {
                    foundAction = true;
                    actionScript = o.Actions[i].Script;
                    break;
                }
            }

            if (foundAction)
            {
                ExecuteScript(actionScript, ctx, id);
            }
            else
            {
                // Now check for a property
                propertyExists = IsYes(GetObjectProperty(action, id, true, false));

                if (!propertyExists)
                {
                    // Show error message
                    if (doOpen)
                    {
                        PlayerErrorMessage(PlayerError.CantOpen, ctx);
                    }
                    else
                    {
                        PlayerErrorMessage(PlayerError.CantClose, ctx);
                    }
                }
                else
                {
                    textToPrint = GetObjectProperty(action, id, false, false);
                    if (string.IsNullOrEmpty(textToPrint))
                    {
                        // Show default message
                        if (doOpen)
                        {
                            PlayerErrorMessage(PlayerError.DefaultOpen, ctx);
                        }
                        else
                        {
                            PlayerErrorMessage(PlayerError.DefaultClose, ctx);
                        }
                    }
                    else
                    {
                        Print(textToPrint, ctx);
                    }

                    DoOpenClose(id, doOpen, true, ctx);

                }
            }

        }

        private void ExecuteSelectCase(string script, Context ctx)
        {
            // ScriptLine passed will look like this:
            // select case <whatever> do <!intprocX>
            // with all the case statements in the intproc.

            string afterLine = GetAfterParameter(script);

            if (!BeginsWith(afterLine, "do <!intproc"))
            {
                LogASLError("No case block specified for '" + script + "'", LogType.WarningError);
                return;
            }

            string blockName = GetParameter(afterLine, ctx);
            var block = DefineBlockParam("procedure", blockName);
            string checkValue = GetParameter(script, ctx);
            bool caseMatch = false;

            for (int i = block.StartLine + 1, loopTo = block.EndLine - 1; i <= loopTo; i++)
            {
                // Go through all the cases until we find the one that matches

                if (!string.IsNullOrEmpty(_lines[i]))
                {
                    if (!BeginsWith(_lines[i], "case "))
                    {
                        LogASLError("Invalid line in 'select case' block: '" + _lines[i] + "'", LogType.WarningError);
                    }
                    else
                    {
                        string caseScript = "";

                        if (BeginsWith(_lines[i], "case else "))
                        {
                            caseMatch = true;
                            caseScript = GetEverythingAfter(_lines[i], "case else ");
                        }
                        else
                        {
                            string thisCase = GetParameter(_lines[i], ctx);
                            bool finished = false;

                            do
                            {
                                int SCP = Strings.InStr(thisCase, ";");
                                if (SCP == 0)
                                {
                                    SCP = Strings.Len(thisCase) + 1;
                                    finished = true;
                                }

                                string condition = Strings.Trim(Strings.Left(thisCase, SCP - 1));
                                if ((condition ?? "") == (checkValue ?? ""))
                                {
                                    caseScript = GetAfterParameter(_lines[i]);
                                    caseMatch = true;
                                    finished = true;
                                }
                                else
                                {
                                    thisCase = Strings.Mid(thisCase, SCP + 1);
                                }
                            }
                            while (!finished);
                        }

                        if (caseMatch)
                        {
                            ExecuteScript(caseScript, ctx);
                            return;
                        }
                    }
                }
            }

        }

        private bool ExecVerb(string cmd, Context ctx, bool libCommands = false)
        {
            DefineBlock gameBlock;
            bool foundVerb = false;
            string verbProperty = "";
            string script = "";
            string verbsList;
            string thisVerb = "";
            int scp;
            int id;
            string verbObject = "";
            string verbTag;
            string thisScript = "";

            if (!libCommands)
            {
                verbTag = "verb ";
            }
            else
            {
                verbTag = "lib verb ";
            }

            gameBlock = GetDefineBlock("game");
            for (int i = gameBlock.StartLine + 1, loopTo = gameBlock.EndLine - 1; i <= loopTo; i++)
            {
                if (BeginsWith(_lines[i], verbTag))
                {
                    verbsList = GetParameter(_lines[i], ctx);

                    // The property or action the verb uses is either after a colon,
                    // or it's the first (or only) verb on the line.

                    int colonPos = Strings.InStr(verbsList, ":");
                    if (colonPos != 0)
                    {
                        verbProperty = Strings.LCase(Strings.Trim(Strings.Mid(verbsList, colonPos + 1)));
                        verbsList = Strings.Trim(Strings.Left(verbsList, colonPos - 1));
                    }
                    else
                    {
                        scp = Strings.InStr(verbsList, ";");
                        if (scp == 0)
                        {
                            verbProperty = Strings.LCase(verbsList);
                        }
                        else
                        {
                            verbProperty = Strings.LCase(Strings.Trim(Strings.Left(verbsList, scp - 1)));
                        }
                    }

                    // Now let's see if this matches:

                    do
                    {
                        scp = Strings.InStr(verbsList, ";");
                        if (scp == 0)
                        {
                            thisVerb = Strings.LCase(verbsList);
                        }
                        else
                        {
                            thisVerb = Strings.LCase(Strings.Trim(Strings.Left(verbsList, scp - 1)));
                        }

                        if (BeginsWith(cmd, thisVerb + " "))
                        {
                            foundVerb = true;
                            verbObject = GetEverythingAfter(cmd, thisVerb + " ");
                            script = Strings.Trim(Strings.Mid(_lines[i], Strings.InStr(_lines[i], ">") + 1));
                        }

                        if (scp != 0)
                        {
                            verbsList = Strings.Trim(Strings.Mid(verbsList, scp + 1));
                        }
                    }
                    while (!(scp == 0 | string.IsNullOrEmpty(Strings.Trim(verbsList)) | foundVerb));

                    if (foundVerb)
                        break;

                }
            }

            if (foundVerb)
            {

                id = Disambiguate(verbObject, "inventory;" + _currentRoom, ctx);

                if (id < 0)
                {
                    if (id != -2)
                        PlayerErrorMessage(PlayerError.BadThing, ctx);
                    _badCmdBefore = thisVerb;
                }
                else
                {
                    SetStringContents("quest.error.article", _objs[id].Article, ctx);

                    bool foundAction = false;

                    // Now see if this object has the relevant action or property
                    var o = _objs[id];
                    for (int i = 1, loopTo1 = o.NumberActions; i <= loopTo1; i++)
                    {
                        if ((Strings.LCase(o.Actions[i].ActionName) ?? "") == (verbProperty ?? ""))
                        {
                            foundAction = true;
                            thisScript = o.Actions[i].Script;
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(thisScript))
                    {
                        // Avoid an RTE "this array is fixed or temporarily locked"
                        ExecuteScript(thisScript, ctx, id);
                    }

                    if (!foundAction)
                    {
                        // Check properties for a message
                        for (int i = 1, loopTo2 = o.NumberProperties; i <= loopTo2; i++)
                        {
                            if ((Strings.LCase(o.Properties[i].PropertyName) ?? "") == (verbProperty ?? ""))
                            {
                                foundAction = true;
                                Print(o.Properties[i].PropertyValue, ctx);
                                break;
                            }
                        }
                    }

                    if (!foundAction)
                    {
                        // Execute the default script from the verb definition
                        ExecuteScript(script, ctx);
                    }
                }
            }

            return foundVerb;
        }

        private ExpressionResult ExpressionHandler(string expr)
        {
            int openBracketPos, endBracketPos;
            var res = new ExpressionResult();

            // Find brackets, recursively call ExpressionHandler
            do
            {
                openBracketPos = Strings.InStr(expr, "(");
                if (openBracketPos != 0)
                {
                    // Find equivalent closing bracket
                    int BracketCount = 1;
                    endBracketPos = 0;
                    for (int i = openBracketPos + 1, loopTo = Strings.Len(expr); i <= loopTo; i++)
                    {
                        if (Strings.Mid(expr, i, 1) == "(")
                        {
                            BracketCount = BracketCount + 1;
                        }
                        else if (Strings.Mid(expr, i, 1) == ")")
                        {
                            BracketCount = BracketCount - 1;
                        }

                        if (BracketCount == 0)
                        {
                            endBracketPos = i;
                            break;
                        }
                    }

                    if (endBracketPos != 0)
                    {
                        var NestedResult = ExpressionHandler(Strings.Mid(expr, openBracketPos + 1, endBracketPos - openBracketPos - 1));
                        if (NestedResult.Success != ExpressionSuccess.OK)
                        {
                            res.Success = NestedResult.Success;
                            res.Message = NestedResult.Message;
                            return res;
                        }

                        expr = Strings.Left(expr, openBracketPos - 1) + " " + NestedResult.Result + " " + Strings.Mid(expr, endBracketPos + 1);
                    }

                    else
                    {
                        res.Message = "Missing closing bracket";
                        res.Success = ExpressionSuccess.Fail;
                        return res;

                    }
                }
            }
            while (openBracketPos != 0);

            // Split expression into elements, e.g.:
            // 2 + 3 * 578.2 / 36
            // E O E O EEEEE O EE      where E=Element, O=Operator

            int numElements = 1;
            string[] elements;
            elements = new string[2];
            int numOperators = 0;
            string[] operators = new string[1];
            bool newElement;

            string obscuredExpr = ObscureNumericExps(expr);

            for (int i = 1, loopTo1 = Strings.Len(expr); i <= loopTo1; i++)
            {
                switch (Strings.Mid(obscuredExpr, i, 1) ?? "")
                {
                    case "+":
                    case "*":
                    case "/":
                        {
                            newElement = true;
                            break;
                        }
                    case "-":
                        {
                            // A minus often means subtraction, so it's a new element. But sometimes
                            // it just denotes a negative number. In this case, the current element will
                            // be empty.

                            if (string.IsNullOrEmpty(Strings.Trim(elements[numElements])))
                            {
                                newElement = false;
                            }
                            else
                            {
                                newElement = true;
                            }

                            break;
                        }

                    default:
                        {
                            newElement = false;
                            break;
                        }
                }

                if (newElement)
                {
                    numElements = numElements + 1;
                    Array.Resize(ref elements, numElements + 1);

                    numOperators = numOperators + 1;
                    Array.Resize(ref operators, numOperators + 1);
                    operators[numOperators] = Strings.Mid(expr, i, 1);
                }
                else
                {
                    elements[numElements] = elements[numElements] + Strings.Mid(expr, i, 1);
                }
            }

            // Check Elements are numeric, and trim spaces
            for (int i = 1, loopTo2 = numElements; i <= loopTo2; i++)
            {
                elements[i] = Strings.Trim(elements[i]);

                if (!Information.IsNumeric(elements[i]))
                {
                    res.Message = "Syntax error evaluating expression - non-numeric element '" + elements[i] + "'";
                    res.Success = ExpressionSuccess.Fail;
                    return res;
                }
            }

            int opNum = 0;

            var result = default(double);
            do
            {
                // Go through the Operators array to find next calculation to perform

                for (int i = 1, loopTo3 = numOperators; i <= loopTo3; i++)
                {
                    if (operators[i] == "/" | operators[i] == "*")
                    {
                        opNum = i;
                        break;
                    }
                }

                if (opNum == 0)
                {
                    for (int i = 1, loopTo4 = numOperators; i <= loopTo4; i++)
                    {
                        if (operators[i] == "+" | operators[i] == "-")
                        {
                            opNum = i;
                            break;
                        }
                    }
                }

                // If OpNum is still 0, there are no calculations left to do.

                if (opNum != 0)
                {

                    double val1 = Conversions.ToDouble(elements[opNum]);
                    double val2 = Conversions.ToDouble(elements[opNum + 1]);

                    switch (operators[opNum] ?? "")
                    {
                        case "/":
                            {
                                if (val2 == 0d)
                                {
                                    res.Message = "Division by zero";
                                    res.Success = ExpressionSuccess.Fail;
                                    return res;
                                }
                                result = val1 / val2;
                                break;
                            }
                        case "*":
                            {
                                result = val1 * val2;
                                break;
                            }
                        case "+":
                            {
                                result = val1 + val2;
                                break;
                            }
                        case "-":
                            {
                                result = val1 - val2;
                                break;
                            }
                    }

                    elements[opNum] = result.ToString();

                    // Remove this operator, and Elements(OpNum+1) from the arrays
                    for (int i = opNum, loopTo5 = numOperators - 1; i <= loopTo5; i++)
                        operators[i] = operators[i + 1];
                    for (int i = opNum + 1, loopTo6 = numElements - 1; i <= loopTo6; i++)
                        elements[i] = elements[i + 1];
                    numOperators = numOperators - 1;
                    numElements = numElements - 1;
                    Array.Resize(ref operators, numOperators + 1);
                    Array.Resize(ref elements, numElements + 1);

                }
            }
            while (!(opNum == 0 | numOperators == 0));

            res.Success = ExpressionSuccess.OK;
            res.Result = elements[1];
            return res;

        }

        private string ListContents(int id, Context ctx)
        {
            // Returns a formatted list of the contents of a container.
            // If the list action causes a script to be run instead, ListContents
            // returns "<script>"

            int[] contentsIDs = new int[1];

            if (!IsYes(GetObjectProperty("container", id, true, false)))
            {
                return "";
            }

            if (!IsYes(GetObjectProperty("opened", id, true, false)) & !IsYes(GetObjectProperty("transparent", id, true, false)) & !IsYes(GetObjectProperty("surface", id, true, false)))
            {
                // Container is closed, so return "list closed" property if there is one.

                if (DoAction(id, "list closed", ctx, false))
                {
                    return "<script>";
                }
                else
                {
                    return GetObjectProperty("list closed", id, false, false);
                }
            }

            // populate contents string

            int numContents = 0;

            for (int i = 1, loopTo = _numberObjs; i <= loopTo; i++)
            {
                if (_objs[i].Exists & _objs[i].Visible)
                {
                    if ((Strings.LCase(GetObjectProperty("parent", i, false, false)) ?? "") == (Strings.LCase(_objs[id].ObjectName) ?? ""))
                    {
                        numContents = numContents + 1;
                        Array.Resize(ref contentsIDs, numContents + 1);
                        contentsIDs[numContents] = i;
                    }
                }
            }

            string contents = "";

            if (numContents > 0)
            {
                // Check if list property is set.

                if (DoAction(id, "list", ctx, false))
                {
                    return "<script>";
                }

                if (IsYes(GetObjectProperty("list", id, true, false)))
                {
                    // Read header, if any
                    string listString = GetObjectProperty("list", id, false, false);
                    bool displayList = true;

                    if (!string.IsNullOrEmpty(listString))
                    {
                        if (Strings.Right(listString, 1) == ":")
                        {
                            contents = Strings.Left(listString, Strings.Len(listString) - 1) + " ";
                        }
                        else
                        {
                            // If header doesn't end in a colon, then the header is the only text to print
                            contents = listString;
                            displayList = false;
                        }
                    }
                    else
                    {
                        contents = Strings.UCase(Strings.Left(_objs[id].Article, 1)) + Strings.Mid(_objs[id].Article, 2) + " contains ";
                    }

                    if (displayList)
                    {
                        for (int i = 1, loopTo1 = numContents; i <= loopTo1; i++)
                        {
                            if (i > 1)
                            {
                                if (i < numContents)
                                {
                                    contents = contents + ", ";
                                }
                                else
                                {
                                    contents = contents + " and ";
                                }
                            }

                            var o = _objs[contentsIDs[i]];
                            if (!string.IsNullOrEmpty(o.Prefix))
                                contents = contents + o.Prefix;
                            if (!string.IsNullOrEmpty(o.ObjectAlias))
                            {
                                contents = contents + "|b" + o.ObjectAlias + "|xb";
                            }
                            else
                            {
                                contents = contents + "|b" + o.ObjectName + "|xb";
                            }
                            if (!string.IsNullOrEmpty(o.Suffix))
                                contents = contents + " " + o.Suffix;
                        }
                    }

                    return contents + ".";
                }
                // The "list" property is not set, so do not list contents.
                return "";
            }

            // Container is empty, so return "list empty" property if there is one.

            if (DoAction(id, "list empty", ctx, false))
            {
                return "<script>";
            }
            else
            {
                return GetObjectProperty("list empty", id, false, false);
            }

        }

        private string ObscureNumericExps(string s)
        {
            // Obscures + or - next to E in Double-type variables with exponents
            // e.g. 2.345E+20 becomes 2.345EX20
            // This stops huge numbers breaking parsing of maths functions

            int ep;
            string result = s;

            int pos = 1;
            do
            {
                ep = Strings.InStr(pos, result, "E");
                if (ep != 0)
                {
                    result = Strings.Left(result, ep) + "X" + Strings.Mid(result, ep + 2);
                    pos = ep + 2;
                }
            }
            while (ep != 0);

            return result;
        }

        private void ProcessListInfo(string line, int id)
        {
            var listInfo = new TextAction();
            string propName = "";

            if (BeginsWith(line, "list closed <"))
            {
                listInfo.Type = TextActionType.Text;
                listInfo.Data = GetParameter(line, _nullContext);
                propName = "list closed";
            }
            else if (Strings.Trim(line) == "list closed off")
            {
                // default for list closed is off anyway
                return;
            }
            else if (BeginsWith(line, "list closed"))
            {
                listInfo.Type = TextActionType.Script;
                listInfo.Data = GetEverythingAfter(line, "list closed");
                propName = "list closed";
            }


            else if (BeginsWith(line, "list empty <"))
            {
                listInfo.Type = TextActionType.Text;
                listInfo.Data = GetParameter(line, _nullContext);
                propName = "list empty";
            }
            else if (Strings.Trim(line) == "list empty off")
            {
                // default for list empty is off anyway
                return;
            }
            else if (BeginsWith(line, "list empty"))
            {
                listInfo.Type = TextActionType.Script;
                listInfo.Data = GetEverythingAfter(line, "list empty");
                propName = "list empty";
            }


            else if (Strings.Trim(line) == "list off")
            {
                AddToObjectProperties("not list", id, _nullContext);
                return;
            }
            else if (BeginsWith(line, "list <"))
            {
                listInfo.Type = TextActionType.Text;
                listInfo.Data = GetParameter(line, _nullContext);
                propName = "list";
            }
            else if (BeginsWith(line, "list "))
            {
                listInfo.Type = TextActionType.Script;
                listInfo.Data = GetEverythingAfter(line, "list ");
                propName = "list";
            }

            if (!string.IsNullOrEmpty(propName))
            {
                if (listInfo.Type == TextActionType.Text)
                {
                    AddToObjectProperties(propName + "=" + listInfo.Data, id, _nullContext);
                }
                else
                {
                    AddToObjectActions("<" + propName + "> " + listInfo.Data, id, _nullContext);
                }
            }
        }

        private string GetHTMLColour(string colour, string defaultColour)
        {
            // Converts a Quest foreground or background colour setting into an HTML colour

            colour = Strings.LCase(colour);

            if (string.IsNullOrEmpty(colour) | colour == "0")
                colour = defaultColour;

            switch (colour ?? "")
            {
                case "white":
                    {
                        return "FFFFFF";
                    }
                case "black":
                    {
                        return "000000";
                    }
                case "blue":
                    {
                        return "0000FF";
                    }
                case "yellow":
                    {
                        return "FFFF00";
                    }
                case "red":
                    {
                        return "FF0000";
                    }
                case "green":
                    {
                        return "00FF00";
                    }

                default:
                    {
                        return colour;
                    }
            }
        }

        private void DoPrint(string text)
        {
            PrintText?.Invoke(_textFormatter.OutputHTML(text));
        }

        private void DestroyExit(string exitData, Context ctx)
        {
            string fromRoom = "";
            string toRoom = "";
            int roomId = default, exitId = default;

            int scp = Strings.InStr(exitData, ";");
            if (scp == 0)
            {
                LogASLError("No exit name specified in 'destroy exit <" + exitData + ">'");
                return;
            }

            LegacyASL.RoomExit roomExit;
            if (_gameAslVersion >= 410)
            {
                roomExit = FindExit(exitData);
                if (roomExit is null)
                {
                    LogASLError("Can't find exit in 'destroy exit <" + exitData + ">'");
                    return;
                }

                roomExit.GetParent().RemoveExit(ref roomExit);
            }

            else
            {

                fromRoom = Strings.LCase(Strings.Trim(Strings.Left(exitData, scp - 1)));
                toRoom = Strings.Trim(Strings.Mid(exitData, scp + 1));

                // Find From Room:
                bool found = false;

                for (int i = 1, loopTo = _numberRooms; i <= loopTo; i++)
                {
                    if ((Strings.LCase(_rooms[i].RoomName) ?? "") == (fromRoom ?? ""))
                    {
                        found = true;
                        roomId = i;
                        break;
                    }
                }

                if (!found)
                {
                    LogASLError("No such room '" + fromRoom + "'");
                    return;
                }

                found = false;
                var r = _rooms[roomId];

                for (int i = 1, loopTo1 = r.NumberPlaces; i <= loopTo1; i++)
                {
                    if ((r.Places[i].PlaceName ?? "") == (toRoom ?? ""))
                    {
                        exitId = i;
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    for (int i = exitId, loopTo2 = r.NumberPlaces - 1; i <= loopTo2; i++)
                        r.Places[i] = r.Places[i + 1];
                    Array.Resize(ref r.Places, r.NumberPlaces);
                    r.NumberPlaces = r.NumberPlaces - 1;
                }
            }

            // Update quest.* vars and obj list
            ShowRoomInfo(_currentRoom, ctx, true);
            UpdateObjectList(ctx);

            AddToChangeLog("room " + fromRoom, "destroy exit " + toRoom);
        }

        private void DoClear()
        {
            _player.ClearScreen();
        }

        private void DoWait()
        {
            _player.DoWait();
            ChangeState(State.Waiting);

            lock (_waitLock)
                System.Threading.Monitor.Wait(_waitLock);
        }

        private void ExecuteFlag(string line, Context ctx)
        {
            string propertyString = "";

            if (BeginsWith(line, "on "))
            {
                propertyString = GetParameter(line, ctx);
            }
            else if (BeginsWith(line, "off "))
            {
                propertyString = "not " + GetParameter(line, ctx);
            }

            // Game object always has ObjID 1
            AddToObjectProperties(propertyString, 1, ctx);
        }

        private bool ExecuteIfFlag(string flag)
        {
            // Game ObjID is 1
            return GetObjectProperty(flag, 1, true) == "yes";
        }

        private void ExecuteIncDec(string line, Context ctx)
        {
            string variable;
            double change;
            string @param = GetParameter(line, ctx);

            int sc = Strings.InStr(@param, ";");
            if (sc == 0)
            {
                change = 1d;
                variable = @param;
            }
            else
            {
                change = Conversion.Val(Strings.Mid(@param, sc + 1));
                variable = Strings.Trim(Strings.Left(@param, sc - 1));
            }

            double value = GetNumericContents(variable, ctx, true);
            if (value <= -32766)
                value = 0d;

            if (BeginsWith(line, "inc "))
            {
                value = value + change;
            }
            else if (BeginsWith(line, "dec "))
            {
                value = value - change;
            }

            var arrayIndex = GetArrayIndex(variable, ctx);
            SetNumericVariableContents(arrayIndex.Name, value, ctx, arrayIndex.Index);
        }

        private string ExtractFile(string @file)
        {
            int length = default, startPos = default;
            var extracted = default(bool);
            var resId = default(int);

            if (string.IsNullOrEmpty(_resourceFile))
                return "";

            // Find file in catalog

            bool found = false;

            for (int i = 1, loopTo = _numResources; i <= loopTo; i++)
            {
                if ((Strings.LCase(@file) ?? "") == (Strings.LCase(_resources[i].ResourceName) ?? ""))
                {
                    found = true;
                    startPos = _resources[i].ResourceStart + _resourceOffset;
                    length = _resources[i].ResourceLength;
                    extracted = _resources[i].Extracted;
                    resId = i;
                    break;
                }
            }

            if (!found)
            {
                LogASLError("Unable to extract '" + @file + "' - not present in resources.", LogType.WarningError);
                return null;
            }

            string fileName = System.IO.Path.Combine(_tempFolder, @file);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fileName));

            if (!extracted)
            {
                // Extract file from cached CAS data
                string fileData = Strings.Mid(_casFileData, startPos, length);

                // Write file to temp dir
                System.IO.File.WriteAllText(fileName, fileData, System.Text.Encoding.GetEncoding(1252));

                _resources[resId].Extracted = true;
            }

            return fileName;
        }

        private void AddObjectAction(int id, string name, string script, bool noUpdate = false)
        {

            // Use NoUpdate in e.g. AddToGiveInfo, otherwise ObjectActionUpdate will call
            // AddToGiveInfo again leading to a big loop

            var actionNum = default(int);
            bool foundExisting = false;

            var o = _objs[id];

            for (int i = 1, loopTo = o.NumberActions; i <= loopTo; i++)
            {
                if ((o.Actions[i].ActionName ?? "") == (name ?? ""))
                {
                    foundExisting = true;
                    actionNum = i;
                    break;
                }
            }

            if (!foundExisting)
            {
                o.NumberActions = o.NumberActions + 1;
                Array.Resize(ref o.Actions, o.NumberActions + 1);
                o.Actions[o.NumberActions] = new ActionType();
                actionNum = o.NumberActions;
            }

            o.Actions[actionNum].ActionName = name;
            o.Actions[actionNum].Script = script;

            ObjectActionUpdate(id, name, script, noUpdate);
        }

        private void AddToChangeLog(string appliesTo, string changeData)
        {
            _gameChangeData.NumberChanges = _gameChangeData.NumberChanges + 1;
            Array.Resize(ref _gameChangeData.ChangeData, _gameChangeData.NumberChanges + 1);
            _gameChangeData.ChangeData[_gameChangeData.NumberChanges] = new ChangeType();
            _gameChangeData.ChangeData[_gameChangeData.NumberChanges].AppliesTo = appliesTo;
            _gameChangeData.ChangeData[_gameChangeData.NumberChanges].Change = changeData;
        }

        private void AddToObjectChangeLog(LegacyASL.ChangeLog.AppliesTo appliesToType, string appliesTo, string element, string changeData)
        {
            LegacyASL.ChangeLog changeLog;

            // NOTE: We're only actually ever using the object changelog.
            // Rooms only get logged for creating rooms and creating/destroying exits, so we don't
            // need the refactored ChangeLog component for those.

            switch (appliesToType)
            {
                case LegacyASL.ChangeLog.AppliesTo.Object:
                    {
                        changeLog = _changeLogObjects;
                        break;
                    }
                case LegacyASL.ChangeLog.AppliesTo.Room:
                    {
                        changeLog = _changeLogRooms;
                        break;
                    }

                default:
                    {
                        throw new ArgumentOutOfRangeException();
                    }
            }

            changeLog.AddItem(ref appliesTo, ref element, ref changeData);
        }

        private void AddToGiveInfo(int id, string giveData)
        {
            GiveType giveType;
            string actionName;
            string actionScript;

            var o = _objs[id];

            if (BeginsWith(giveData, "to "))
            {
                giveData = GetEverythingAfter(giveData, "to ");
                if (BeginsWith(giveData, "anything "))
                {
                    o.GiveToAnything = GetEverythingAfter(giveData, "anything ");
                    AddObjectAction(id, "give to anything", o.GiveToAnything, true);
                    return;
                }
                else
                {
                    giveType = GiveType.GiveToSomething;
                    actionName = "give to ";
                }
            }
            else if (BeginsWith(giveData, "anything "))
            {
                o.GiveAnything = GetEverythingAfter(giveData, "anything ");

                AddObjectAction(id, "give anything", o.GiveAnything, true);
                return;
            }
            else
            {
                giveType = GiveType.GiveSomethingTo;
                actionName = "give ";
            }

            if (Strings.Left(Strings.Trim(giveData), 1) == "<")
            {
                string name = GetParameter(giveData, _nullContext);
                var dataId = default(int);

                actionName = actionName + "'" + name + "'";

                bool found = false;
                for (int i = 1, loopTo = o.NumberGiveData; i <= loopTo; i++)
                {
                    if (o.GiveData[i].GiveType == giveType & (Strings.LCase(o.GiveData[i].GiveObject) ?? "") == (Strings.LCase(name) ?? ""))
                    {
                        dataId = i;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    o.NumberGiveData = o.NumberGiveData + 1;
                    Array.Resize(ref o.GiveData, o.NumberGiveData + 1);
                    o.GiveData[o.NumberGiveData] = new GiveDataType();
                    dataId = o.NumberGiveData;
                }

                int EP = Strings.InStr(giveData, ">");
                o.GiveData[dataId].GiveType = giveType;
                o.GiveData[dataId].GiveObject = name;
                o.GiveData[dataId].GiveScript = Strings.Mid(giveData, EP + 2);

                actionScript = o.GiveData[dataId].GiveScript;
                AddObjectAction(id, actionName, actionScript, true);
            }
        }

        internal void AddToObjectActions(string actionInfo, int id, Context ctx)
        {
            var actionNum = default(int);
            bool foundExisting = false;

            string name = Strings.LCase(GetParameter(actionInfo, ctx));
            int ep = Strings.InStr(actionInfo, ">");
            if (ep == Strings.Len(actionInfo))
            {
                LogASLError("No script given for '" + name + "' action data", LogType.WarningError);
                return;
            }

            string script = Strings.Trim(Strings.Mid(actionInfo, ep + 1));

            var o = _objs[id];

            for (int i = 1, loopTo = o.NumberActions; i <= loopTo; i++)
            {
                if ((o.Actions[i].ActionName ?? "") == (name ?? ""))
                {
                    foundExisting = true;
                    actionNum = i;
                    break;
                }
            }

            if (!foundExisting)
            {
                o.NumberActions = o.NumberActions + 1;
                Array.Resize(ref o.Actions, o.NumberActions + 1);
                o.Actions[o.NumberActions] = new ActionType();
                actionNum = o.NumberActions;
            }

            o.Actions[actionNum].ActionName = name;
            o.Actions[actionNum].Script = script;

            ObjectActionUpdate(id, name, script);
        }

        private void AddToObjectAltNames(string altNames, int id)
        {
            var o = _objs[id];

            do
            {
                int endPos = Strings.InStr(altNames, ";");
                if (endPos == 0)
                    endPos = Strings.Len(altNames) + 1;
                string curName = Strings.Trim(Strings.Left(altNames, endPos - 1));

                if (!string.IsNullOrEmpty(curName))
                {
                    o.NumberAltNames = o.NumberAltNames + 1;
                    Array.Resize(ref o.AltNames, o.NumberAltNames + 1);
                    o.AltNames[o.NumberAltNames] = curName;
                }

                altNames = Strings.Mid(altNames, endPos + 1);
            }
            while (!string.IsNullOrEmpty(Strings.Trim(altNames)));
        }

        internal void AddToObjectProperties(string propertyInfo, int id, Context ctx)
        {
            if (id == 0)
                return;

            if (Strings.Right(propertyInfo, 1) != ";")
            {
                propertyInfo = propertyInfo + ";";
            }

            var num = default(int);
            do
            {
                int scp = Strings.InStr(propertyInfo, ";");
                string info = Strings.Left(propertyInfo, scp - 1);
                propertyInfo = Strings.Trim(Strings.Mid(propertyInfo, scp + 1));

                string name, value;

                if (string.IsNullOrEmpty(info))
                    break;

                int ep = Strings.InStr(info, "=");
                if (ep != 0)
                {
                    name = Strings.Trim(Strings.Left(info, ep - 1));
                    value = Strings.Trim(Strings.Mid(info, ep + 1));
                }
                else
                {
                    name = info;
                    value = "";
                }

                bool falseProperty = false;
                if (BeginsWith(name, "not ") & string.IsNullOrEmpty(value))
                {
                    falseProperty = true;
                    name = GetEverythingAfter(name, "not ");
                }

                var o = _objs[id];

                bool found = false;
                for (int i = 1, loopTo = o.NumberProperties; i <= loopTo; i++)
                {
                    if ((Strings.LCase(o.Properties[i].PropertyName) ?? "") == (Strings.LCase(name) ?? ""))
                    {
                        found = true;
                        num = i;
                        i = o.NumberProperties;
                    }
                }

                if (!found)
                {
                    o.NumberProperties = o.NumberProperties + 1;
                    Array.Resize(ref o.Properties, o.NumberProperties + 1);
                    o.Properties[o.NumberProperties] = new PropertyType();
                    num = o.NumberProperties;
                }

                if (falseProperty)
                {
                    o.Properties[num].PropertyName = "";
                }
                else
                {
                    o.Properties[num].PropertyName = name;
                    o.Properties[num].PropertyValue = value;
                }

                this.AddToObjectChangeLog(LegacyASL.ChangeLog.AppliesTo.Object, _objs[id].ObjectName, name, "properties " + info);

                switch (name ?? "")
                {
                    case "alias":
                        {
                            if (o.IsRoom)
                            {
                                _rooms[o.CorresRoomId].RoomAlias = value;
                            }
                            else
                            {
                                o.ObjectAlias = value;
                            }
                            if (_gameFullyLoaded)
                            {
                                UpdateObjectList(ctx);
                                UpdateItems(ctx);
                            }

                            break;
                        }
                    case "prefix":
                        {
                            if (o.IsRoom)
                            {
                                _rooms[o.CorresRoomId].Prefix = value;
                            }
                            else if (!string.IsNullOrEmpty(value))
                            {
                                o.Prefix = value + " ";
                            }
                            else
                            {
                                o.Prefix = "";
                            }

                            break;
                        }
                    case "indescription":
                        {
                            if (o.IsRoom)
                                _rooms[o.CorresRoomId].InDescription = value;
                            break;
                        }
                    case "description":
                        {
                            if (o.IsRoom)
                            {
                                _rooms[o.CorresRoomId].Description.Data = value;
                                _rooms[o.CorresRoomId].Description.Type = TextActionType.Text;
                            }

                            break;
                        }
                    case "look":
                        {
                            if (o.IsRoom)
                            {
                                _rooms[o.CorresRoomId].Look = value;
                            }

                            break;
                        }
                    case "suffix":
                        {
                            o.Suffix = value;
                            break;
                        }
                    case "displaytype":
                        {
                            o.DisplayType = value;
                            if (_gameFullyLoaded)
                                UpdateObjectList(ctx);
                            break;
                        }
                    case "gender":
                        {
                            o.Gender = value;
                            break;
                        }
                    case "article":
                        {
                            o.Article = value;
                            break;
                        }
                    case "detail":
                        {
                            o.Detail = value;
                            break;
                        }
                    case "hidden":
                        {
                            if (falseProperty)
                            {
                                o.Exists = true;
                            }
                            else
                            {
                                o.Exists = false;
                            }

                            if (_gameFullyLoaded)
                                UpdateObjectList(ctx);
                            break;
                        }
                    case "invisible":
                        {
                            if (falseProperty)
                            {
                                o.Visible = true;
                            }
                            else
                            {
                                o.Visible = false;
                            }

                            if (_gameFullyLoaded)
                                UpdateObjectList(ctx);
                            break;
                        }
                    case "take":
                        {
                            if (_gameAslVersion >= 392)
                            {
                                if (falseProperty)
                                {
                                    o.Take.Type = TextActionType.Nothing;
                                }
                                else if (string.IsNullOrEmpty(value))
                                {
                                    o.Take.Type = TextActionType.Default;
                                }
                                else
                                {
                                    o.Take.Type = TextActionType.Text;
                                    o.Take.Data = value;
                                }
                            }

                            break;
                        }
                }
            }
            while (Strings.Len(Strings.Trim(propertyInfo)) != 0);
        }

        private void AddToUseInfo(int id, string useData)
        {
            UseType useType;

            var o = _objs[id];

            if (BeginsWith(useData, "on "))
            {
                useData = GetEverythingAfter(useData, "on ");
                if (BeginsWith(useData, "anything "))
                {
                    o.UseOnAnything = GetEverythingAfter(useData, "anything ");
                    return;
                }
                else
                {
                    useType = UseType.UseOnSomething;
                }
            }
            else if (BeginsWith(useData, "anything "))
            {
                o.UseAnything = GetEverythingAfter(useData, "anything ");
                return;
            }
            else
            {
                useType = UseType.UseSomethingOn;
            }

            if (Strings.Left(Strings.Trim(useData), 1) == "<")
            {
                string objectName = GetParameter(useData, _nullContext);
                var dataId = default(int);
                bool found = false;

                for (int i = 1, loopTo = o.NumberUseData; i <= loopTo; i++)
                {
                    if (o.UseData[i].UseType == useType & (Strings.LCase(o.UseData[i].UseObject) ?? "") == (Strings.LCase(objectName) ?? ""))
                    {
                        dataId = i;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    o.NumberUseData = o.NumberUseData + 1;
                    Array.Resize(ref o.UseData, o.NumberUseData + 1);
                    o.UseData[o.NumberUseData] = new UseDataType();
                    dataId = o.NumberUseData;
                }

                int ep = Strings.InStr(useData, ">");
                o.UseData[dataId].UseType = useType;
                o.UseData[dataId].UseObject = objectName;
                o.UseData[dataId].UseScript = Strings.Mid(useData, ep + 2);
            }
            else
            {
                o.Use = Strings.Trim(useData);
            }

        }

        private string CapFirst(string s)
        {
            return Strings.UCase(Strings.Left(s, 1)) + Strings.Mid(s, 2);
        }

        private string ConvertVarsIn(string s, Context ctx)
        {
            return GetParameter("<" + s + ">", ctx);
        }

        private bool DisambObjHere(Context ctx, int id, string firstPlace, bool twoPlaces = false, string secondPlace = "", bool isExit = false)
        {

            var isSeen = default(bool);
            bool onlySeen = false;

            if (firstPlace == "game")
            {
                firstPlace = "";
                if (secondPlace == "seen")
                {
                    twoPlaces = false;
                    secondPlace = "";
                    onlySeen = true;
                    int roomObjId = _rooms[GetRoomID(_objs[id].ContainerRoom, ctx)].ObjId;

                    if (_objs[id].ContainerRoom == "inventory")
                    {
                        isSeen = true;
                    }
                    else if (IsYes(GetObjectProperty("visited", roomObjId, true, false)))
                    {
                        isSeen = true;
                    }
                    else if (IsYes(GetObjectProperty("seen", id, true, false)))
                    {
                        isSeen = true;
                    }

                }
            }

            if ((twoPlaces == false & ((Strings.LCase(_objs[id].ContainerRoom) ?? "") == (Strings.LCase(firstPlace) ?? "") | string.IsNullOrEmpty(firstPlace)) | twoPlaces == true & ((Strings.LCase(_objs[id].ContainerRoom) ?? "") == (Strings.LCase(firstPlace) ?? "") | (Strings.LCase(_objs[id].ContainerRoom) ?? "") == (Strings.LCase(secondPlace) ?? ""))) & _objs[id].Exists == true & _objs[id].IsExit == isExit)
            {
                if (!onlySeen)
                {
                    return true;
                }
                return isSeen;
            }

            return false;
        }

        private void ExecClone(string cloneString, Context ctx)
        {
            int id;
            string newName, cloneTo;

            int scp = Strings.InStr(cloneString, ";");
            if (scp == 0)
            {
                LogASLError("No new object name specified in 'clone <" + cloneString + ">", LogType.WarningError);
                return;
            }
            else
            {
                string objectToClone = Strings.Trim(Strings.Left(cloneString, scp - 1));
                id = GetObjectIdNoAlias(objectToClone);

                int SC2 = Strings.InStr(scp + 1, cloneString, ";");
                if (SC2 == 0)
                {
                    cloneTo = _objs[id].ContainerRoom;
                    newName = Strings.Trim(Strings.Mid(cloneString, scp + 1));
                }
                else
                {
                    cloneTo = Strings.Trim(Strings.Mid(cloneString, SC2 + 1));
                    newName = Strings.Trim(Strings.Mid(cloneString, scp + 1, SC2 - scp - 1));
                }
            }

            _numberObjs = _numberObjs + 1;
            Array.Resize(ref _objs, _numberObjs + 1);
            _objs[_numberObjs] = new ObjectType();
            _objs[_numberObjs] = _objs[id];
            _objs[_numberObjs].ContainerRoom = cloneTo;
            _objs[_numberObjs].ObjectName = newName;

            if (_objs[id].IsRoom)
            {
                // This is a room so create the corresponding room as well

                _numberRooms = _numberRooms + 1;
                Array.Resize(ref _rooms, _numberRooms + 1);
                _rooms[_numberRooms] = new RoomType();
                _rooms[_numberRooms] = _rooms[_objs[id].CorresRoomId];
                _rooms[_numberRooms].RoomName = newName;
                _rooms[_numberRooms].ObjId = _numberObjs;

                _objs[_numberObjs].CorresRoom = newName;
                _objs[_numberObjs].CorresRoomId = _numberRooms;

                AddToChangeLog("room " + newName, "create");
            }
            else
            {
                AddToChangeLog("object " + newName, "create " + _objs[_numberObjs].ContainerRoom);
            }

            UpdateObjectList(ctx);
        }

        private void ExecOops(string correction, Context ctx)
        {
            if (!string.IsNullOrEmpty(_badCmdBefore))
            {
                if (string.IsNullOrEmpty(_badCmdAfter))
                {
                    ExecCommand(_badCmdBefore + " " + correction, ctx, false);
                }
                else
                {
                    ExecCommand(_badCmdBefore + " " + correction + " " + _badCmdAfter, ctx, false);
                }
            }
        }

        private void ExecType(string typeData, Context ctx)
        {
            var id = default(int);
            var found = default(bool);
            int scp = Strings.InStr(typeData, ";");

            if (scp == 0)
            {
                LogASLError("No type name given in 'type <" + typeData + ">'");
                return;
            }

            string objName = Strings.Trim(Strings.Left(typeData, scp - 1));
            string typeName = Strings.Trim(Strings.Mid(typeData, scp + 1));

            for (int i = 1, loopTo = _numberObjs; i <= loopTo; i++)
            {
                if ((Strings.LCase(_objs[i].ObjectName) ?? "") == (Strings.LCase(objName) ?? ""))
                {
                    found = true;
                    id = i;
                    break;
                }
            }

            if (!found)
            {
                LogASLError("No such object in 'type <" + typeData + ">'");
                return;
            }

            var o = _objs[id];

            o.NumberTypesIncluded = o.NumberTypesIncluded + 1;
            Array.Resize(ref o.TypesIncluded, o.NumberTypesIncluded + 1);
            o.TypesIncluded[o.NumberTypesIncluded] = typeName;

            var propertyData = GetPropertiesInType(typeName);
            AddToObjectProperties(propertyData.Properties, id, ctx);
            for (int i = 1, loopTo1 = propertyData.NumberActions; i <= loopTo1; i++)
                AddObjectAction(id, propertyData.Actions[i].ActionName, propertyData.Actions[i].Script);

            // New as of Quest 4.0. Fixes bug that "if type" would fail for any
            // parent types included by the "type" command.
            for (int i = 1, loopTo2 = propertyData.NumberTypesIncluded; i <= loopTo2; i++)
            {
                o.NumberTypesIncluded = o.NumberTypesIncluded + 1;
                Array.Resize(ref o.TypesIncluded, o.NumberTypesIncluded + 1);
                o.TypesIncluded[o.NumberTypesIncluded] = propertyData.TypesIncluded[i];
            }
        }

        private bool ExecuteIfAction(string actionData)
        {
            var id = default(int);

            int scp = Strings.InStr(actionData, ";");

            if (scp == 0)
            {
                LogASLError("No action name given in condition 'action <" + actionData + ">' ...", LogType.WarningError);
                return false;
            }

            string objName = Strings.Trim(Strings.Left(actionData, scp - 1));
            string actionName = Strings.Trim(Strings.Mid(actionData, scp + 1));
            bool found = false;

            for (int i = 1, loopTo = _numberObjs; i <= loopTo; i++)
            {
                if ((Strings.LCase(_objs[i].ObjectName) ?? "") == (Strings.LCase(objName) ?? ""))
                {
                    found = true;
                    id = i;
                    break;
                }
            }

            if (!found)
            {
                LogASLError("No such object '" + objName + "' in condition 'action <" + actionData + ">' ...", LogType.WarningError);
                return false;
            }

            var o = _objs[id];

            for (int i = 1, loopTo1 = o.NumberActions; i <= loopTo1; i++)
            {
                if ((Strings.LCase(o.Actions[i].ActionName) ?? "") == (Strings.LCase(actionName) ?? ""))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ExecuteIfType(string typeData)
        {
            var id = default(int);

            int scp = Strings.InStr(typeData, ";");

            if (scp == 0)
            {
                LogASLError("No type name given in condition 'type <" + typeData + ">' ...", LogType.WarningError);
                return false;
            }

            string objName = Strings.Trim(Strings.Left(typeData, scp - 1));
            string typeName = Strings.Trim(Strings.Mid(typeData, scp + 1));

            bool found = false;

            for (int i = 1, loopTo = _numberObjs; i <= loopTo; i++)
            {
                if ((Strings.LCase(_objs[i].ObjectName) ?? "") == (Strings.LCase(objName) ?? ""))
                {
                    found = true;
                    id = i;
                    break;
                }
            }

            if (!found)
            {
                LogASLError("No such object '" + objName + "' in condition 'type <" + typeData + ">' ...", LogType.WarningError);
                return false;
            }

            var o = _objs[id];

            for (int i = 1, loopTo1 = o.NumberTypesIncluded; i <= loopTo1; i++)
            {
                if ((Strings.LCase(o.TypesIncluded[i]) ?? "") == (Strings.LCase(typeName) ?? ""))
                {
                    return true;
                }
            }

            return false;
        }

        private class ArrayResult
        {
            public string Name;
            public int Index;
        }

        private ArrayResult GetArrayIndex(string varName, Context ctx)
        {
            var result = new ArrayResult();

            if (Strings.InStr(varName, "[") == 0 | Strings.InStr(varName, "]") == 0)
            {
                result.Name = varName;
                return result;
            }

            int beginPos = Strings.InStr(varName, "[");
            int endPos = Strings.InStr(varName, "]");
            string data = Strings.Mid(varName, beginPos + 1, endPos - beginPos - 1);

            if (Information.IsNumeric(data))
            {
                result.Index = Conversions.ToInteger(data);
            }
            else
            {
                result.Index = (int)Math.Round(GetNumericContents(data, ctx));
            }

            result.Name = Strings.Left(varName, beginPos - 1);
            return result;
        }

        internal int Disambiguate(string name, string containedIn, Context ctx, bool isExit = false)
        {
            // Returns object ID being referred to by player.
            // Returns -1 if object doesn't exist, calling function
            // then expected to print relevant error.
            // Returns -2 if "it" meaningless, prints own error.
            // If it returns an object ID, it also sets quest.lastobject to the name
            // of the object referred to.
            // If ctx.AllowRealNamesInCommand is True, will allow an object's real
            // name to be used even when the object has an alias - this is used when
            // Disambiguate has been called after an "exec" command to prevent the
            // player having to choose an object from the disambiguation menu twice

            int numberCorresIds = 0;
            int[] idNumbers = new int[1];
            string firstPlace;
            string secondPlace = "";
            bool twoPlaces;
            string[] descriptionText;
            string[] validNames;
            int numValidNames;

            name = Strings.Trim(name);

            SetStringContents("quest.lastobject", "", ctx);

            if (Strings.InStr(containedIn, ";") != 0)
            {
                int scp = Strings.InStr(containedIn, ";");
                twoPlaces = true;
                firstPlace = Strings.Trim(Strings.Left(containedIn, scp - 1));
                secondPlace = Strings.Trim(Strings.Mid(containedIn, scp + 1));
            }
            else
            {
                twoPlaces = false;
                firstPlace = containedIn;
            }

            if (ctx.AllowRealNamesInCommand)
            {
                for (int i = 1, loopTo = _numberObjs; i <= loopTo; i++)
                {
                    if (DisambObjHere(ctx, i, firstPlace, twoPlaces, secondPlace))
                    {
                        if ((Strings.LCase(_objs[i].ObjectName) ?? "") == (Strings.LCase(name) ?? ""))
                        {
                            SetStringContents("quest.lastobject", _objs[i].ObjectName, ctx);
                            return i;
                        }
                    }
                }
            }

            // If player uses "it", "them" etc. as name:
            if (name == "it" | name == "them" | name == "this" | name == "those" | name == "these" | name == "that")
            {
                SetStringContents("quest.error.pronoun", name, ctx);
                if (_lastIt != 0 & _lastItMode == ItType.Inanimate & DisambObjHere(ctx, _lastIt, firstPlace, twoPlaces, secondPlace))
                {
                    SetStringContents("quest.lastobject", _objs[_lastIt].ObjectName, ctx);
                    return _lastIt;
                }
                else
                {
                    PlayerErrorMessage(PlayerError.BadPronoun, ctx);
                    return -2;
                }
            }
            else if (name == "him")
            {
                SetStringContents("quest.error.pronoun", name, ctx);
                if (_lastIt != 0 & _lastItMode == ItType.Male & DisambObjHere(ctx, _lastIt, firstPlace, twoPlaces, secondPlace))
                {
                    SetStringContents("quest.lastobject", _objs[_lastIt].ObjectName, ctx);
                    return _lastIt;
                }
                else
                {
                    PlayerErrorMessage(PlayerError.BadPronoun, ctx);
                    return -2;
                }
            }
            else if (name == "her")
            {
                SetStringContents("quest.error.pronoun", name, ctx);
                if (_lastIt != 0 & _lastItMode == ItType.Female & DisambObjHere(ctx, _lastIt, firstPlace, twoPlaces, secondPlace))
                {
                    SetStringContents("quest.lastobject", _objs[_lastIt].ObjectName, ctx);
                    return _lastIt;
                }
                else
                {
                    PlayerErrorMessage(PlayerError.BadPronoun, ctx);
                    return -2;
                }
            }

            _thisTurnIt = 0;

            if (BeginsWith(name, "the "))
            {
                name = GetEverythingAfter(name, "the ");
            }

            for (int i = 1, loopTo1 = _numberObjs; i <= loopTo1; i++)
            {
                if (DisambObjHere(ctx, i, firstPlace, twoPlaces, secondPlace, isExit))
                {
                    numValidNames = _objs[i].NumberAltNames + 1;
                    validNames = new string[numValidNames + 1];
                    validNames[1] = _objs[i].ObjectAlias;
                    for (int j = 1, loopTo2 = _objs[i].NumberAltNames; j <= loopTo2; j++)
                        validNames[j + 1] = _objs[i].AltNames[j];

                    for (int j = 1, loopTo3 = numValidNames; j <= loopTo3; j++)
                    {
                        if ((Strings.LCase(validNames[j]) ?? "") == (Strings.LCase(name) ?? "") | ("the " + Strings.LCase(name) ?? "") == (Strings.LCase(validNames[j]) ?? ""))
                        {
                            numberCorresIds = numberCorresIds + 1;
                            Array.Resize(ref idNumbers, numberCorresIds + 1);
                            idNumbers[numberCorresIds] = i;
                            j = numValidNames;
                        }
                    }
                }
            }

            if (_gameAslVersion >= 391 & numberCorresIds == 0 & _useAbbreviations & Strings.Len(name) > 0)
            {
                // Check for abbreviated object names

                for (int i = 1, loopTo4 = _numberObjs; i <= loopTo4; i++)
                {
                    if (DisambObjHere(ctx, i, firstPlace, twoPlaces, secondPlace, isExit))
                    {
                        string thisName;
                        if (!string.IsNullOrEmpty(_objs[i].ObjectAlias))
                            thisName = Strings.LCase(_objs[i].ObjectAlias);
                        else
                            thisName = Strings.LCase(_objs[i].ObjectName);
                        if (_gameAslVersion >= 410)
                        {
                            if (!string.IsNullOrEmpty(_objs[i].Prefix))
                                thisName = Strings.Trim(Strings.LCase(_objs[i].Prefix)) + " " + thisName;
                            if (!string.IsNullOrEmpty(_objs[i].Suffix))
                                thisName = thisName + " " + Strings.Trim(Strings.LCase(_objs[i].Suffix));
                        }
                        if (Strings.InStr(" " + thisName, " " + Strings.LCase(name)) != 0)
                        {
                            numberCorresIds = numberCorresIds + 1;
                            Array.Resize(ref idNumbers, numberCorresIds + 1);
                            idNumbers[numberCorresIds] = i;
                        }
                    }
                }
            }

            if (numberCorresIds == 1)
            {
                SetStringContents("quest.lastobject", _objs[idNumbers[1]].ObjectName, ctx);
                _thisTurnIt = idNumbers[1];

                switch (_objs[idNumbers[1]].Article ?? "")
                {
                    case "him":
                        {
                            _thisTurnItMode = ItType.Male;
                            break;
                        }
                    case "her":
                        {
                            _thisTurnItMode = ItType.Female;
                            break;
                        }

                    default:
                        {
                            _thisTurnItMode = ItType.Inanimate;
                            break;
                        }
                }

                return idNumbers[1];
            }
            else if (numberCorresIds > 1)
            {
                descriptionText = new string[numberCorresIds + 1];

                string question = "Please select which " + name + " you mean:";
                Print("- |i" + question + "|xi", ctx);

                var menuItems = new Dictionary<string, string>();

                for (int i = 1, loopTo5 = numberCorresIds; i <= loopTo5; i++)
                {
                    descriptionText[i] = _objs[idNumbers[i]].Detail;
                    if (string.IsNullOrEmpty(descriptionText[i]))
                    {
                        if (string.IsNullOrEmpty(_objs[idNumbers[i]].Prefix))
                        {
                            descriptionText[i] = _objs[idNumbers[i]].ObjectAlias;
                        }
                        else
                        {
                            descriptionText[i] = _objs[idNumbers[i]].Prefix + _objs[idNumbers[i]].ObjectAlias;
                        }
                    }

                    menuItems.Add(i.ToString(), descriptionText[i]);

                }

                var mnu = new MenuData(question, menuItems, false);
                string response = ShowMenu(mnu);

                _choiceNumber = Conversions.ToInteger(response);

                SetStringContents("quest.lastobject", _objs[idNumbers[_choiceNumber]].ObjectName, ctx);

                _thisTurnIt = idNumbers[_choiceNumber];

                switch (_objs[idNumbers[_choiceNumber]].Article ?? "")
                {
                    case "him":
                        {
                            _thisTurnItMode = ItType.Male;
                            break;
                        }
                    case "her":
                        {
                            _thisTurnItMode = ItType.Female;
                            break;
                        }

                    default:
                        {
                            _thisTurnItMode = ItType.Inanimate;
                            break;
                        }
                }

                Print("- " + descriptionText[_choiceNumber] + "|n", ctx);

                return idNumbers[_choiceNumber];
            }

            _thisTurnIt = _lastIt;
            SetStringContents("quest.error.object", name, ctx);
            return -1;
        }

        private string DisplayStatusVariableInfo(int id, VarType @type, Context ctx)
        {
            string displayData = "";
            int ep;

            if (type == VarType.String)
            {
                displayData = ConvertVarsIn(_stringVariable[id].DisplayString, ctx);
                ep = Strings.InStr(displayData, "!");

                if (ep != 0)
                {
                    displayData = Strings.Left(displayData, ep - 1) + _stringVariable[id].VariableContents[0] + Strings.Mid(displayData, ep + 1);
                }
            }
            else if (type == VarType.Numeric)
            {
                if (_numericVariable[id].NoZeroDisplay & Conversion.Val(_numericVariable[id].VariableContents[0]) == 0d)
                {
                    return "";
                }
                displayData = ConvertVarsIn(_numericVariable[id].DisplayString, ctx);
                ep = Strings.InStr(displayData, "!");

                if (ep != 0)
                {
                    displayData = Strings.Left(displayData, ep - 1) + _numericVariable[id].VariableContents[0] + Strings.Mid(displayData, ep + 1);
                }

                if (Strings.InStr(displayData, "*") > 0)
                {
                    int firstStar = Strings.InStr(displayData, "*");
                    int secondStar = Strings.InStr(firstStar + 1, displayData, "*");
                    string beforeStar = Strings.Left(displayData, firstStar - 1);
                    string afterStar = Strings.Mid(displayData, secondStar + 1);
                    string betweenStar = Strings.Mid(displayData, firstStar + 1, secondStar - firstStar - 1);

                    if (Conversions.ToDouble(_numericVariable[id].VariableContents[0]) != 1d)
                    {
                        displayData = beforeStar + betweenStar + afterStar;
                    }
                    else
                    {
                        displayData = beforeStar + afterStar;
                    }
                }
            }

            return displayData;
        }

        internal bool DoAction(int id, string action, Context ctx, bool logError = true)
        {
            var found = default(bool);
            string script = "";

            var o = _objs[id];

            for (int i = 1, loopTo = o.NumberActions; i <= loopTo; i++)
            {
                if ((o.Actions[i].ActionName ?? "") == (Strings.LCase(action) ?? ""))
                {
                    found = true;
                    script = o.Actions[i].Script;
                    break;
                }
            }

            if (!found)
            {
                if (logError)
                    LogASLError("No such action '" + action + "' defined for object '" + o.ObjectName + "'");
                return false;
            }

            var newCtx = CopyContext(ctx);
            newCtx.CallingObjectId = id;

            ExecuteScript(script, newCtx, id);

            return true;
        }

        public bool HasAction(int id, string action)
        {
            var o = _objs[id];

            for (int i = 1, loopTo = o.NumberActions; i <= loopTo; i++)
            {
                if ((o.Actions[i].ActionName ?? "") == (Strings.LCase(action) ?? ""))
                {
                    return true;
                }
            }

            return false;
        }

        private void ExecForEach(string scriptLine, Context ctx)
        {
            string inLocation, scriptToRun;
            var isExit = default(bool);
            var isRoom = default(bool);

            if (BeginsWith(scriptLine, "object "))
            {
                scriptLine = GetEverythingAfter(scriptLine, "object ");
                if (!BeginsWith(scriptLine, "in "))
                {
                    LogASLError("Expected 'in' in 'for each object " + ReportErrorLine(scriptLine) + "'", LogType.WarningError);
                    return;
                }
            }
            else if (BeginsWith(scriptLine, "exit "))
            {
                scriptLine = GetEverythingAfter(scriptLine, "exit ");
                if (!BeginsWith(scriptLine, "in "))
                {
                    LogASLError("Expected 'in' in 'for each exit " + ReportErrorLine(scriptLine) + "'", LogType.WarningError);
                    return;
                }
                isExit = true;
            }
            else if (BeginsWith(scriptLine, "room "))
            {
                scriptLine = GetEverythingAfter(scriptLine, "room ");
                if (!BeginsWith(scriptLine, "in "))
                {
                    LogASLError("Expected 'in' in 'for each room " + ReportErrorLine(scriptLine) + "'", LogType.WarningError);
                    return;
                }
                isRoom = true;
            }
            else
            {
                LogASLError("Unknown type in 'for each " + ReportErrorLine(scriptLine) + "'", LogType.WarningError);
                return;
            }

            scriptLine = GetEverythingAfter(scriptLine, "in ");

            if (BeginsWith(scriptLine, "game "))
            {
                inLocation = "";
                scriptToRun = GetEverythingAfter(scriptLine, "game ");
            }
            else
            {
                inLocation = Strings.LCase(GetParameter(scriptLine, ctx));
                int bracketPos = Strings.InStr(scriptLine, ">");
                scriptToRun = Strings.Trim(Strings.Mid(scriptLine, bracketPos + 1));
            }

            for (int i = 1, loopTo = _numberObjs; i <= loopTo; i++)
            {
                if (string.IsNullOrEmpty(inLocation) | (Strings.LCase(_objs[i].ContainerRoom) ?? "") == (inLocation ?? ""))
                {
                    if (_objs[i].IsRoom == isRoom & _objs[i].IsExit == isExit)
                    {
                        SetStringContents("quest.thing", _objs[i].ObjectName, ctx);
                        ExecuteScript(scriptToRun, ctx);
                    }
                }
            }
        }

        private void ExecuteAction(string data, Context ctx)
        {
            string actionName;
            string script;
            var actionNum = default(int);
            var id = default(int);
            bool foundExisting = false;
            bool foundObject = false;

            string @param = GetParameter(data, ctx);
            int scp = Strings.InStr(@param, ";");
            if (scp == 0)
            {
                LogASLError("No action name specified in 'action " + data + "'", LogType.WarningError);
                return;
            }

            string objName = Strings.Trim(Strings.Left(@param, scp - 1));
            actionName = Strings.Trim(Strings.Mid(@param, scp + 1));

            int ep = Strings.InStr(data, ">");
            if (ep == Strings.Len(Strings.Trim(data)))
            {
                script = "";
            }
            else
            {
                script = Strings.Trim(Strings.Mid(data, ep + 1));
            }

            for (int i = 1, loopTo = _numberObjs; i <= loopTo; i++)
            {
                if ((Strings.LCase(_objs[i].ObjectName) ?? "") == (Strings.LCase(objName) ?? ""))
                {
                    foundObject = true;
                    id = i;
                    break;
                }
            }

            if (!foundObject)
            {
                LogASLError("No such object '" + objName + "' in 'action " + data + "'", LogType.WarningError);
                return;
            }

            var o = _objs[id];

            for (int i = 1, loopTo1 = o.NumberActions; i <= loopTo1; i++)
            {
                if ((o.Actions[i].ActionName ?? "") == (actionName ?? ""))
                {
                    foundExisting = true;
                    actionNum = i;
                    break;
                }
            }

            if (!foundExisting)
            {
                o.NumberActions = o.NumberActions + 1;
                Array.Resize(ref o.Actions, o.NumberActions + 1);
                o.Actions[o.NumberActions] = new ActionType();
                actionNum = o.NumberActions;
            }

            o.Actions[actionNum].ActionName = actionName;
            o.Actions[actionNum].Script = script;

            ObjectActionUpdate(id, actionName, script);
        }

        private bool ExecuteCondition(string condition, Context ctx)
        {
            bool result = default, thisNot;

            if (BeginsWith(condition, "not "))
            {
                thisNot = true;
                condition = GetEverythingAfter(condition, "not ");
            }
            else
            {
                thisNot = false;
            }

            if (BeginsWith(condition, "got "))
            {
                result = ExecuteIfGot(GetParameter(condition, ctx));
            }
            else if (BeginsWith(condition, "has "))
            {
                result = ExecuteIfHas(GetParameter(condition, ctx));
            }
            else if (BeginsWith(condition, "ask "))
            {
                result = ExecuteIfAsk(GetParameter(condition, ctx));
            }
            else if (BeginsWith(condition, "is "))
            {
                result = ExecuteIfIs(GetParameter(condition, ctx));
            }
            else if (BeginsWith(condition, "here "))
            {
                result = ExecuteIfHere(GetParameter(condition, ctx), ctx);
            }
            else if (BeginsWith(condition, "exists "))
            {
                result = ExecuteIfExists(GetParameter(condition, ctx), false);
            }
            else if (BeginsWith(condition, "real "))
            {
                result = ExecuteIfExists(GetParameter(condition, ctx), true);
            }
            else if (BeginsWith(condition, "property "))
            {
                result = ExecuteIfProperty(GetParameter(condition, ctx));
            }
            else if (BeginsWith(condition, "action "))
            {
                result = ExecuteIfAction(GetParameter(condition, ctx));
            }
            else if (BeginsWith(condition, "type "))
            {
                result = ExecuteIfType(GetParameter(condition, ctx));
            }
            else if (BeginsWith(condition, "flag "))
            {
                result = ExecuteIfFlag(GetParameter(condition, ctx));
            }

            if (thisNot)
                result = !result;

            return result;
        }

        private bool ExecuteConditions(string list, Context ctx)
        {
            var conditions = default(string[]);
            int numConditions = 0;
            var operations = default(string[]);
            string obscuredConditionList = ObliterateParameters(list);
            int pos = 1;
            bool isFinalCondition = false;

            do
            {
                numConditions = numConditions + 1;
                Array.Resize(ref conditions, numConditions + 1);
                Array.Resize(ref operations, numConditions + 1);

                string nextCondition = "AND";
                int nextConditionPos = Strings.InStr(pos, obscuredConditionList, "and ");
                if (nextConditionPos == 0)
                {
                    nextConditionPos = Strings.InStr(pos, obscuredConditionList, "or ");
                    nextCondition = "OR";
                }

                if (nextConditionPos == 0)
                {
                    nextConditionPos = Strings.Len(obscuredConditionList) + 2;
                    isFinalCondition = true;
                    nextCondition = "FINAL";
                }

                string thisCondition = Strings.Trim(Strings.Mid(list, pos, nextConditionPos - pos - 1));
                conditions[numConditions] = thisCondition;
                operations[numConditions] = nextCondition;

                // next condition starts from space after and/or
                pos = Strings.InStr(nextConditionPos, obscuredConditionList, " ");
            }
            while (!isFinalCondition);

            operations[0] = "AND";
            bool result = true;

            for (int i = 1, loopTo = numConditions; i <= loopTo; i++)
            {
                bool thisResult = ExecuteCondition(conditions[i], ctx);

                if (operations[i - 1] == "AND")
                {
                    result = thisResult & result;
                }
                else if (operations[i - 1] == "OR")
                {
                    result = thisResult | result;
                }
            }

            return result;
        }

        private void ExecuteCreate(string data, Context ctx)
        {
            string newName;

            if (BeginsWith(data, "room "))
            {
                newName = GetParameter(data, ctx);
                _numberRooms = _numberRooms + 1;
                Array.Resize(ref _rooms, _numberRooms + 1);
                _rooms[_numberRooms] = new RoomType();
                _rooms[_numberRooms].RoomName = newName;

                _numberObjs = _numberObjs + 1;
                Array.Resize(ref _objs, _numberObjs + 1);
                _objs[_numberObjs] = new ObjectType();
                _objs[_numberObjs].ObjectName = newName;
                _objs[_numberObjs].IsRoom = true;
                _objs[_numberObjs].CorresRoom = newName;
                _objs[_numberObjs].CorresRoomId = _numberRooms;

                _rooms[_numberRooms].ObjId = _numberObjs;

                AddToChangeLog("room " + newName, "create");

                if (_gameAslVersion >= 410)
                {
                    AddToObjectProperties(_defaultRoomProperties.Properties, _numberObjs, ctx);
                    for (int j = 1, loopTo = _defaultRoomProperties.NumberActions; j <= loopTo; j++)
                        AddObjectAction(_numberObjs, _defaultRoomProperties.Actions[j].ActionName, _defaultRoomProperties.Actions[j].Script);

                    _rooms[_numberRooms].Exits = new LegacyASL.RoomExits(this);
                    _rooms[_numberRooms].Exits.SetObjId(_rooms[_numberRooms].ObjId);
                }
            }

            else if (BeginsWith(data, "object "))
            {
                string paramData = GetParameter(data, ctx);
                int scp = Strings.InStr(paramData, ";");
                string containerRoom;

                if (scp == 0)
                {
                    newName = paramData;
                    containerRoom = "";
                }
                else
                {
                    newName = Strings.Trim(Strings.Left(paramData, scp - 1));
                    containerRoom = Strings.Trim(Strings.Mid(paramData, scp + 1));
                }

                _numberObjs = _numberObjs + 1;
                Array.Resize(ref _objs, _numberObjs + 1);
                _objs[_numberObjs] = new ObjectType();

                var o = _objs[_numberObjs];
                o.ObjectName = newName;
                o.ObjectAlias = newName;
                o.ContainerRoom = containerRoom;
                o.Exists = true;
                o.Visible = true;
                o.Gender = "it";
                o.Article = "it";

                AddToChangeLog("object " + newName, "create " + _objs[_numberObjs].ContainerRoom);

                if (_gameAslVersion >= 410)
                {
                    AddToObjectProperties(_defaultProperties.Properties, _numberObjs, ctx);
                    for (int j = 1, loopTo1 = _defaultProperties.NumberActions; j <= loopTo1; j++)
                        AddObjectAction(_numberObjs, _defaultProperties.Actions[j].ActionName, _defaultProperties.Actions[j].Script);
                }

                if (!_gameLoading)
                    UpdateObjectList(ctx);
            }

            else if (BeginsWith(data, "exit "))
            {
                ExecuteCreateExit(data, ctx);
            }
        }

        private void ExecuteCreateExit(string data, Context ctx)
        {
            string scrRoom;
            string destRoom = "";
            var destId = default(int);
            string exitData = GetEverythingAfter(data, "exit ");
            string newName = GetParameter(data, ctx);
            int scp = Strings.InStr(newName, ";");

            if (_gameAslVersion < 410)
            {
                if (scp == 0)
                {
                    LogASLError("No exit destination given in 'create exit " + exitData + "'", LogType.WarningError);
                    return;
                }
            }

            if (scp == 0)
            {
                scrRoom = Strings.Trim(newName);
            }
            else
            {
                scrRoom = Strings.Trim(Strings.Left(newName, scp - 1));
            }
            int srcId = GetRoomID(scrRoom, ctx);

            if (srcId == 0)
            {
                LogASLError("No such room '" + scrRoom + "'", LogType.WarningError);
                return;
            }

            if (_gameAslVersion < 410)
            {
                // only do destination room check for ASL <410, as can now have scripts on dynamically
                // created exits, so the destination doesn't necessarily have to exist.

                destRoom = Strings.Trim(Strings.Mid(newName, scp + 1));
                if (!string.IsNullOrEmpty(destRoom))
                {
                    destId = GetRoomID(destRoom, ctx);

                    if (destId == 0)
                    {
                        LogASLError("No such room '" + destRoom + "'", LogType.WarningError);
                        return;
                    }
                }
            }

            // If it's a "go to" exit, check if it already exists:
            bool exists = false;
            if (BeginsWith(exitData, "<"))
            {
                if (_gameAslVersion >= 410)
                {
                    exists = _rooms[srcId].Exits.GetPlaces().ContainsKey(destRoom);
                }
                else
                {
                    for (int i = 1, loopTo = _rooms[srcId].NumberPlaces; i <= loopTo; i++)
                    {
                        if ((Strings.LCase(_rooms[srcId].Places[i].PlaceName) ?? "") == (Strings.LCase(destRoom) ?? ""))
                        {
                            exists = true;
                            break;
                        }
                    }
                }

                if (exists)
                {
                    LogASLError("Exit from '" + scrRoom + "' to '" + destRoom + "' already exists", LogType.WarningError);
                    return;
                }
            }

            int paramPos = Strings.InStr(exitData, "<");
            string saveData;
            if (paramPos == 0)
            {
                saveData = exitData;
            }
            else
            {
                saveData = Strings.Left(exitData, paramPos - 1);
                // We do this so the changelog doesn't contain unconverted variable names
                saveData = saveData + "<" + GetParameter(exitData, ctx) + ">";
            }
            AddToChangeLog("room " + _rooms[srcId].RoomName, "exit " + saveData);

            var r = _rooms[srcId];

            if (_gameAslVersion >= 410)
            {
                r.Exits.AddExitFromCreateScript(exitData, ref ctx);
            }
            else if (BeginsWith(exitData, "north "))
            {
                r.North.Data = destRoom;
                r.North.Type = TextActionType.Text;
            }
            else if (BeginsWith(exitData, "south "))
            {
                r.South.Data = destRoom;
                r.South.Type = TextActionType.Text;
            }
            else if (BeginsWith(exitData, "east "))
            {
                r.East.Data = destRoom;
                r.East.Type = TextActionType.Text;
            }
            else if (BeginsWith(exitData, "west "))
            {
                r.West.Data = destRoom;
                r.West.Type = TextActionType.Text;
            }
            else if (BeginsWith(exitData, "northeast "))
            {
                r.NorthEast.Data = destRoom;
                r.NorthEast.Type = TextActionType.Text;
            }
            else if (BeginsWith(exitData, "northwest "))
            {
                r.NorthWest.Data = destRoom;
                r.NorthWest.Type = TextActionType.Text;
            }
            else if (BeginsWith(exitData, "southeast "))
            {
                r.SouthEast.Data = destRoom;
                r.SouthEast.Type = TextActionType.Text;
            }
            else if (BeginsWith(exitData, "southwest "))
            {
                r.SouthWest.Data = destRoom;
                r.SouthWest.Type = TextActionType.Text;
            }
            else if (BeginsWith(exitData, "up "))
            {
                r.Up.Data = destRoom;
                r.Up.Type = TextActionType.Text;
            }
            else if (BeginsWith(exitData, "down "))
            {
                r.Down.Data = destRoom;
                r.Down.Type = TextActionType.Text;
            }
            else if (BeginsWith(exitData, "out "))
            {
                r.Out.Text = destRoom;
            }
            else if (BeginsWith(exitData, "<"))
            {
                r.NumberPlaces = r.NumberPlaces + 1;
                Array.Resize(ref r.Places, r.NumberPlaces + 1);
                r.Places[r.NumberPlaces] = new PlaceType();
                r.Places[r.NumberPlaces].PlaceName = destRoom;
            }
            else
            {
                LogASLError("Invalid direction in 'create exit " + exitData + "'", LogType.WarningError);
            }

            if (!_gameLoading)
            {
                // Update quest.doorways variables
                ShowRoomInfo(_currentRoom, ctx, true);

                UpdateObjectList(ctx);

                if (_gameAslVersion < 410)
                {
                    if ((_currentRoom ?? "") == (_rooms[srcId].RoomName ?? ""))
                    {
                        UpdateDoorways(srcId, ctx);
                    }
                    else if ((_currentRoom ?? "") == (_rooms[destId].RoomName ?? ""))
                    {
                        UpdateDoorways(destId, ctx);
                    }
                }
                else
                {
                    // Don't have DestID in ASL410 CreateExit code, so just UpdateDoorways
                    // for current room anyway.
                    UpdateDoorways(GetRoomID(_currentRoom, ctx), ctx);
                }
            }
        }

        private void ExecDrop(string obj, Context ctx)
        {
            bool found;
            int parentId = default, id;

            id = Disambiguate(obj, "inventory", ctx);

            if (id > 0)
            {
                found = true;
            }
            else
            {
                found = false;
            }

            if (!found)
            {
                if (id != -2)
                {
                    if (_gameAslVersion >= 391)
                    {
                        PlayerErrorMessage(PlayerError.NoItem, ctx);
                    }
                    else
                    {
                        PlayerErrorMessage(PlayerError.BadDrop, ctx);
                    }
                }
                _badCmdBefore = "drop";
                return;
            }

            // If object is inside a container, it must be removed before it can be dropped.
            bool isInContainer = false;
            if (_gameAslVersion >= 391)
            {
                if (IsYes(GetObjectProperty("parent", id, true, false)))
                {
                    isInContainer = true;
                    string parent = GetObjectProperty("parent", id, false, false);
                    parentId = GetObjectIdNoAlias(parent);
                }
            }

            bool dropFound = false;
            string dropStatement = "";

            for (int i = _objs[id].DefinitionSectionStart, loopTo = _objs[id].DefinitionSectionEnd; i <= loopTo; i++)
            {
                if (BeginsWith(_lines[i], "drop "))
                {
                    dropStatement = GetEverythingAfter(_lines[i], "drop ");
                    dropFound = true;
                    break;
                }
            }

            SetStringContents("quest.error.article", _objs[id].Article, ctx);

            if (!dropFound | BeginsWith(dropStatement, "everywhere"))
            {
                if (isInContainer)
                {
                    // So, we want to drop an object that's in a container or surface. So first
                    // we have to remove the object from that container.

                    string parentDisplayName;

                    if (!string.IsNullOrEmpty(_objs[parentId].ObjectAlias))
                    {
                        parentDisplayName = _objs[parentId].ObjectAlias;
                    }
                    else
                    {
                        parentDisplayName = _objs[parentId].ObjectName;
                    }

                    Print("(first removing " + _objs[id].Article + " from " + parentDisplayName + ")", ctx);

                    // Try to remove the object
                    ctx.AllowRealNamesInCommand = true;
                    ExecCommand("remove " + _objs[id].ObjectName, ctx, false, dontSetIt: true);

                    if (!string.IsNullOrEmpty(GetObjectProperty("parent", id, false, false)))
                    {
                        // removing the object failed
                        return;
                    }
                }
            }

            if (!dropFound)
            {
                PlayerErrorMessage(PlayerError.DefaultDrop, ctx);
                PlayerItem(_objs[id].ObjectName, false, ctx);
            }
            else if (BeginsWith(dropStatement, "everywhere"))
            {
                PlayerItem(_objs[id].ObjectName, false, ctx);
                if (Strings.InStr(dropStatement, "<") != 0)
                {
                    Print(GetParameter(s: dropStatement, ctx: ctx), ctx);
                }
                else
                {
                    PlayerErrorMessage(PlayerError.DefaultDrop, ctx);
                }
            }
            else if (BeginsWith(dropStatement, "nowhere"))
            {
                if (Strings.InStr(dropStatement, "<") != 0)
                {
                    Print(GetParameter(s: dropStatement, ctx: ctx), ctx);
                }
                else
                {
                    PlayerErrorMessage(PlayerError.CantDrop, ctx);
                }
            }
            else
            {
                ExecuteScript(dropStatement, ctx);
            }
        }

        private void ExecExamine(string command, Context ctx)
        {
            string item = Strings.LCase(Strings.Trim(GetEverythingAfter(command, "examine ")));

            if (string.IsNullOrEmpty(item))
            {
                PlayerErrorMessage(PlayerError.BadExamine, ctx);
                _badCmdBefore = "examine";
                return;
            }

            int id = Disambiguate(item, _currentRoom + ";inventory", ctx);

            if (id <= 0)
            {
                if (id != -2)
                    PlayerErrorMessage(PlayerError.BadThing, ctx);
                _badCmdBefore = "examine";
                return;
            }

            var o = _objs[id];

            // Find "examine" action:
            for (int i = 1, loopTo = o.NumberActions; i <= loopTo; i++)
            {
                if (o.Actions[i].ActionName == "examine")
                {
                    ExecuteScript(o.Actions[i].Script, ctx, id);
                    return;
                }
            }

            // Find "examine" property:
            for (int i = 1, loopTo1 = o.NumberProperties; i <= loopTo1; i++)
            {
                if (o.Properties[i].PropertyName == "examine")
                {
                    Print(o.Properties[i].PropertyValue, ctx);
                    return;
                }
            }

            // Find "examine" tag:
            for (int i = o.DefinitionSectionStart + 1, loopTo2 = _objs[id].DefinitionSectionEnd - 1; i <= loopTo2; i++)
            {
                if (BeginsWith(_lines[i], "examine "))
                {
                    string action = Strings.Trim(GetEverythingAfter(_lines[i], "examine "));
                    if (Strings.Left(action, 1) == "<")
                    {
                        Print(GetParameter(_lines[i], ctx), ctx);
                    }
                    else
                    {
                        ExecuteScript(action, ctx, id);
                    }
                    return;
                }
            }

            DoLook(id, ctx, true);
        }

        private void ExecMoveThing(string data, Thing @type, Context ctx)
        {
            int scp = Strings.InStr(data, ";");
            string name = Strings.Trim(Strings.Left(data, scp - 1));
            string place = Strings.Trim(Strings.Mid(data, scp + 1));
            MoveThing(name, place, type, ctx);
        }

        private void ExecProperty(string data, Context ctx)
        {
            var id = default(int);
            var found = default(bool);
            int scp = Strings.InStr(data, ";");

            if (scp == 0)
            {
                LogASLError("No property data given in 'property <" + data + ">'", LogType.WarningError);
                return;
            }

            string name = Strings.Trim(Strings.Left(data, scp - 1));
            string properties = Strings.Trim(Strings.Mid(data, scp + 1));

            for (int i = 1, loopTo = _numberObjs; i <= loopTo; i++)
            {
                if ((Strings.LCase(_objs[i].ObjectName) ?? "") == (Strings.LCase(name) ?? ""))
                {
                    found = true;
                    id = i;
                    break;
                }
            }

            if (!found)
            {
                LogASLError("No such object in 'property <" + data + ">'", LogType.WarningError);
                return;
            }

            AddToObjectProperties(properties, id, ctx);
        }

        private void ExecuteDo(string procedureName, Context ctx)
        {
            var newCtx = CopyContext(ctx);
            int numParameters = 0;
            bool useNewCtx;

            if (_gameAslVersion >= 392 & Strings.Left(procedureName, 8) == "!intproc")
            {
                // If "do" procedure is run in a new context, context info is not passed to any nested
                // script blocks in braces.

                useNewCtx = false;
            }
            else
            {
                useNewCtx = true;
            }

            if (_gameAslVersion >= 284)
            {
                int obp = Strings.InStr(procedureName, "(");
                var cbp = default(int);
                if (obp != 0)
                {
                    cbp = Strings.InStr(obp + 1, procedureName, ")");
                }

                if (obp != 0 & cbp != 0)
                {
                    string parameters = Strings.Mid(procedureName, obp + 1, cbp - obp - 1);
                    procedureName = Strings.Left(procedureName, obp - 1);

                    parameters = parameters + ";";
                    int pos = 1;
                    do
                    {
                        numParameters = numParameters + 1;
                        int scp = Strings.InStr(pos, parameters, ";");

                        newCtx.NumParameters = numParameters;
                        Array.Resize(ref newCtx.Parameters, numParameters + 1);
                        newCtx.Parameters[numParameters] = Strings.Trim(Strings.Mid(parameters, pos, scp - pos));

                        pos = scp + 1;
                    }
                    while (pos < Strings.Len(parameters));
                }
            }

            var block = DefineBlockParam("procedure", procedureName);
            if (block.StartLine == 0 & block.EndLine == 0)
            {
                LogASLError("No such procedure " + procedureName, LogType.WarningError);
            }
            else
            {
                for (int i = block.StartLine + 1, loopTo = block.EndLine - 1; i <= loopTo; i++)
                {
                    if (!useNewCtx)
                    {
                        ExecuteScript(_lines[i], ctx);
                    }
                    else
                    {
                        ExecuteScript(_lines[i], newCtx);
                        ctx.DontProcessCommand = newCtx.DontProcessCommand;
                    }
                }
            }
        }

        private void ExecuteDoAction(string data, Context ctx)
        {
            var id = default(int);

            int scp = Strings.InStr(data, ";");
            if (scp == 0)
            {
                LogASLError("No action name specified in 'doaction <" + data + ">'");
                return;
            }

            string objName = Strings.LCase(Strings.Trim(Strings.Left(data, scp - 1)));
            string actionName = Strings.Trim(Strings.Mid(data, scp + 1));
            bool found = false;

            for (int i = 1, loopTo = _numberObjs; i <= loopTo; i++)
            {
                if ((Strings.LCase(_objs[i].ObjectName) ?? "") == (objName ?? ""))
                {
                    found = true;
                    id = i;
                    break;
                }
            }

            if (!found)
            {
                LogASLError("No such object '" + objName + "'");
                return;
            }

            DoAction(id, actionName, ctx);
        }

        private bool ExecuteIfHere(string obj, Context ctx)
        {
            if (_gameAslVersion <= 281)
            {
                for (int i = 1, loopTo = _numberChars; i <= loopTo; i++)
                {
                    if ((_chars[i].ContainerRoom ?? "") == (_currentRoom ?? "") & _chars[i].Exists)
                    {
                        if ((Strings.LCase(obj) ?? "") == (Strings.LCase(_chars[i].ObjectName) ?? ""))
                        {
                            return true;
                        }
                    }
                }
            }

            for (int i = 1, loopTo1 = _numberObjs; i <= loopTo1; i++)
            {
                if ((Strings.LCase(_objs[i].ContainerRoom) ?? "") == (Strings.LCase(_currentRoom) ?? "") & _objs[i].Exists)
                {
                    if ((Strings.LCase(obj) ?? "") == (Strings.LCase(_objs[i].ObjectName) ?? ""))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool ExecuteIfExists(string obj, bool realOnly)
        {
            var result = default(bool);
            bool errorReport = false;
            int scp;

            if (Strings.InStr(obj, ";") != 0)
            {
                scp = Strings.InStr(obj, ";");
                if (Strings.LCase(Strings.Trim(Strings.Mid(obj, scp + 1))) == "report")
                {
                    errorReport = true;
                }

                obj = Strings.Left(obj, scp - 1);
            }

            bool found = false;

            if (_gameAslVersion < 281)
            {
                for (int i = 1, loopTo = _numberChars; i <= loopTo; i++)
                {
                    if ((Strings.LCase(obj) ?? "") == (Strings.LCase(_chars[i].ObjectName) ?? ""))
                    {
                        result = _chars[i].Exists;
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                for (int i = 1, loopTo1 = _numberObjs; i <= loopTo1; i++)
                {
                    if ((Strings.LCase(obj) ?? "") == (Strings.LCase(_objs[i].ObjectName) ?? ""))
                    {
                        result = _objs[i].Exists;
                        found = true;
                        break;
                    }
                }
            }

            if (found == false & errorReport)
            {
                LogASLError("No such character/object '" + obj + "'.", LogType.UserError);
            }

            if (found == false)
                result = false;

            if (realOnly)
            {
                return found;
            }

            return result;
        }

        private bool ExecuteIfProperty(string data)
        {
            var id = default(int);
            int scp = Strings.InStr(data, ";");

            if (scp == 0)
            {
                LogASLError("No property name given in condition 'property <" + data + ">' ...", LogType.WarningError);
                return false;
            }

            string objName = Strings.Trim(Strings.Left(data, scp - 1));
            string propertyName = Strings.Trim(Strings.Mid(data, scp + 1));
            bool found = false;

            for (int i = 1, loopTo = _numberObjs; i <= loopTo; i++)
            {
                if ((Strings.LCase(_objs[i].ObjectName) ?? "") == (Strings.LCase(objName) ?? ""))
                {
                    found = true;
                    id = i;
                    break;
                }
            }

            if (!found)
            {
                LogASLError("No such object '" + objName + "' in condition 'property <" + data + ">' ...", LogType.WarningError);
                return false;
            }

            return GetObjectProperty(propertyName, id, true) == "yes";
        }

        private void ExecuteRepeat(string data, Context ctx)
        {
            bool repeatWhileTrue;
            string repeatScript = "";
            int bracketPos;
            string afterBracket;
            bool foundScript = false;

            if (BeginsWith(data, "while "))
            {
                repeatWhileTrue = true;
                data = GetEverythingAfter(data, "while ");
            }
            else if (BeginsWith(data, "until "))
            {
                repeatWhileTrue = false;
                data = GetEverythingAfter(data, "until ");
            }
            else
            {
                LogASLError("Expected 'until' or 'while' in 'repeat " + ReportErrorLine(data) + "'", LogType.WarningError);
                return;
            }

            int pos = 1;
            do
            {
                bracketPos = Strings.InStr(pos, data, ">");
                afterBracket = Strings.Trim(Strings.Mid(data, bracketPos + 1));
                if (!BeginsWith(afterBracket, "and ") & !BeginsWith(afterBracket, "or "))
                {
                    repeatScript = afterBracket;
                    foundScript = true;
                }
                else
                {
                    pos = bracketPos + 1;
                }
            }
            while (!(foundScript | string.IsNullOrEmpty(afterBracket)));

            string conditions = Strings.Trim(Strings.Left(data, bracketPos));
            bool finished = false;

            do
            {
                if (ExecuteConditions(conditions, ctx) == repeatWhileTrue)
                {
                    ExecuteScript(repeatScript, ctx);
                }
                else
                {
                    finished = true;
                }
            }
            while (!(finished | _gameFinished));
        }

        private void ExecuteSetCollectable(string @param, Context ctx)
        {
            double val;
            var id = default(int);
            int scp = Strings.InStr(@param, ";");
            string name = Strings.Trim(Strings.Left(@param, scp - 1));
            string newVal = Strings.Trim(Strings.Right(@param, Strings.Len(@param) - scp));
            bool found = false;

            for (int i = 1, loopTo = _numCollectables; i <= loopTo; i++)
            {
                if ((_collectables[i].Name ?? "") == (name ?? ""))
                {
                    id = i;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                LogASLError("No such collectable '" + @param + "'", LogType.WarningError);
                return;
            }

            string op = Strings.Left(newVal, 1);
            string newValue = Strings.Trim(Strings.Right(newVal, Strings.Len(newVal) - 1));
            if (Information.IsNumeric(newValue))
            {
                val = Conversion.Val(newValue);
            }
            else
            {
                val = GetCollectableAmount(newValue);
            }

            if (op == "+")
            {
                _collectables[id].Value = _collectables[id].Value + val;
            }
            else if (op == "-")
            {
                _collectables[id].Value = _collectables[id].Value - val;
            }
            else if (op == "=")
            {
                _collectables[id].Value = val;
            }

            CheckCollectable(id);
            UpdateItems(ctx);
        }

        private void ExecuteWait(string waitLine, Context ctx)
        {
            if (!string.IsNullOrEmpty(waitLine))
            {
                Print(GetParameter(waitLine, ctx), ctx);
            }
            else if (_gameAslVersion >= 410)
            {
                PlayerErrorMessage(PlayerError.DefaultWait, ctx);
            }
            else
            {
                Print("|nPress a key to continue...", ctx);
            }

            DoWait();
        }

        private void InitFileData(string fileData)
        {
            _fileData = fileData;
            _fileDataPos = 1;
        }

        private string GetNextChunk()
        {
            int nullPos = Strings.InStr(_fileDataPos, _fileData, "\0");
            string result = Strings.Mid(_fileData, _fileDataPos, nullPos - _fileDataPos);

            if (nullPos < Strings.Len(_fileData))
            {
                _fileDataPos = nullPos + 1;
            }

            return result;
        }

        public string GetFileDataChars(int count)
        {
            string result = Strings.Mid(_fileData, _fileDataPos, count);
            _fileDataPos = _fileDataPos + count;
            return result;
        }

        private ActionType GetObjectActions(string actionInfo)
        {
            string name = Strings.LCase(GetParameter(actionInfo, _nullContext));
            int ep = Strings.InStr(actionInfo, ">");
            if (ep == Strings.Len(actionInfo))
            {
                LogASLError("No script given for '" + name + "' action data", LogType.WarningError);
                return new ActionType();
            }

            string script = Strings.Trim(Strings.Mid(actionInfo, ep + 1));
            var result = new ActionType();
            result.ActionName = name;
            result.Script = script;
            return result;
        }

        private int GetObjectId(string name, Context ctx, string room = "")
        {
            var id = default(int);
            bool found = false;

            if (BeginsWith(name, "the "))
            {
                name = GetEverythingAfter(name, "the ");
            }

            for (int i = 1, loopTo = _numberObjs; i <= loopTo; i++)
            {
                if (((Strings.LCase(_objs[i].ObjectName) ?? "") == (Strings.LCase(name) ?? "") | (Strings.LCase(_objs[i].ObjectName) ?? "") == ("the " + Strings.LCase(name) ?? "")) & ((Strings.LCase(_objs[i].ContainerRoom) ?? "") == (Strings.LCase(room) ?? "") | string.IsNullOrEmpty(room)) & _objs[i].Exists == true)
                {
                    id = i;
                    found = true;
                    break;
                }
            }

            if (!found & _gameAslVersion >= 280)
            {
                id = Disambiguate(name, room, ctx);
                if (id > 0)
                    found = true;
            }

            if (found)
            {
                return id;
            }

            return -1;
        }

        private int GetObjectIdNoAlias(string name)
        {
            for (int i = 1, loopTo = _numberObjs; i <= loopTo; i++)
            {
                if ((Strings.LCase(_objs[i].ObjectName) ?? "") == (Strings.LCase(name) ?? ""))
                {
                    return i;
                }
            }

            return 0;
        }

        internal string GetObjectProperty(string name, int id, bool existsOnly = false, bool logError = true)
        {
            string result = "";
            bool found = false;
            var o = _objs[id];

            for (int i = 1, loopTo = o.NumberProperties; i <= loopTo; i++)
            {
                if ((Strings.LCase(o.Properties[i].PropertyName) ?? "") == (Strings.LCase(name) ?? ""))
                {
                    found = true;
                    result = o.Properties[i].PropertyValue;
                    break;
                }
            }

            if (existsOnly)
            {
                if (found)
                {
                    return "yes";
                }
                return "no";
            }

            if (found)
            {
                return result;
            }

            if (logError)
            {
                LogASLError("Object '" + _objs[id].ObjectName + "' has no property '" + name + "'", LogType.WarningError);
                return "!";
            }

            return "";
        }

        private PropertiesActions GetPropertiesInType(string @type, bool err = true)
        {
            var blockId = default(int);
            var propertyList = new PropertiesActions();
            bool found = false;

            for (int i = 1, loopTo = _numberSections; i <= loopTo; i++)
            {
                if (BeginsWith(_lines[_defineBlocks[i].StartLine], "define type"))
                {
                    if ((Strings.LCase(GetParameter(_lines[_defineBlocks[i].StartLine], _nullContext)) ?? "") == (Strings.LCase(type) ?? ""))
                    {
                        blockId = i;
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                if (err)
                {
                    LogASLError("No such type '" + type + "'", LogType.WarningError);
                }
                return new PropertiesActions();
            }

            for (int i = _defineBlocks[blockId].StartLine + 1, loopTo1 = _defineBlocks[blockId].EndLine - 1; i <= loopTo1; i++)
            {
                if (BeginsWith(_lines[i], "type "))
                {
                    string typeName = Strings.LCase(GetParameter(_lines[i], _nullContext));
                    var newProperties = GetPropertiesInType(typeName);
                    propertyList.Properties = propertyList.Properties + newProperties.Properties;
                    Array.Resize(ref propertyList.Actions, propertyList.NumberActions + newProperties.NumberActions + 1);
                    for (int j = propertyList.NumberActions + 1, loopTo2 = propertyList.NumberActions + newProperties.NumberActions; j <= loopTo2; j++)
                    {
                        propertyList.Actions[j] = new ActionType();
                        propertyList.Actions[j].ActionName = newProperties.Actions[j - propertyList.NumberActions].ActionName;
                        propertyList.Actions[j].Script = newProperties.Actions[j - propertyList.NumberActions].Script;
                    }
                    propertyList.NumberActions = propertyList.NumberActions + newProperties.NumberActions;

                    // Add this type name to the TypesIncluded list...
                    propertyList.NumberTypesIncluded = propertyList.NumberTypesIncluded + 1;
                    Array.Resize(ref propertyList.TypesIncluded, propertyList.NumberTypesIncluded + 1);
                    propertyList.TypesIncluded[propertyList.NumberTypesIncluded] = typeName;

                    // and add the names of the types included by it...

                    Array.Resize(ref propertyList.TypesIncluded, propertyList.NumberTypesIncluded + newProperties.NumberTypesIncluded + 1);
                    for (int j = propertyList.NumberTypesIncluded + 1, loopTo3 = propertyList.NumberTypesIncluded + newProperties.NumberTypesIncluded; j <= loopTo3; j++)
                        propertyList.TypesIncluded[j] = newProperties.TypesIncluded[j - propertyList.NumberTypesIncluded];
                    propertyList.NumberTypesIncluded = propertyList.NumberTypesIncluded + newProperties.NumberTypesIncluded;
                }
                else if (BeginsWith(_lines[i], "action "))
                {
                    propertyList.NumberActions = propertyList.NumberActions + 1;
                    Array.Resize(ref propertyList.Actions, propertyList.NumberActions + 1);
                    propertyList.Actions[propertyList.NumberActions] = GetObjectActions(GetEverythingAfter(_lines[i], "action "));
                }
                else if (BeginsWith(_lines[i], "properties "))
                {
                    propertyList.Properties = propertyList.Properties + GetParameter(_lines[i], _nullContext) + ";";
                }
                else if (!string.IsNullOrEmpty(Strings.Trim(_lines[i])))
                {
                    propertyList.Properties = propertyList.Properties + _lines[i] + ";";
                }
            }

            return propertyList;
        }

        internal int GetRoomID(string name, Context ctx)
        {
            if (Strings.InStr(name, "[") > 0)
            {
                var idx = GetArrayIndex(name, ctx);
                name = name + Strings.Trim(Conversion.Str(idx.Index));
            }

            for (int i = 1, loopTo = _numberRooms; i <= loopTo; i++)
            {
                if ((Strings.LCase(_rooms[i].RoomName) ?? "") == (Strings.LCase(name) ?? ""))
                {
                    return i;
                }
            }

            return 0;
        }

        private TextAction GetTextOrScript(string textScript)
        {
            var result = new TextAction();
            textScript = Strings.Trim(textScript);

            if (Strings.Left(textScript, 1) == "<")
            {
                result.Type = TextActionType.Text;
                result.Data = GetParameter(textScript, _nullContext);
            }
            else
            {
                result.Type = TextActionType.Script;
                result.Data = textScript;
            }

            return result;
        }

        private int GetThingNumber(string name, string room, Thing @type)
        {
            // Returns the number in the Chars() or _objs() array of the specified char/obj

            if (type == Thing.Character)
            {
                for (int i = 1, loopTo = _numberChars; i <= loopTo; i++)
                {
                    if (!string.IsNullOrEmpty(room) & (Strings.LCase(_chars[i].ObjectName) ?? "") == (Strings.LCase(name) ?? "") & (Strings.LCase(_chars[i].ContainerRoom) ?? "") == (Strings.LCase(room) ?? "") | string.IsNullOrEmpty(room) & (Strings.LCase(_chars[i].ObjectName) ?? "") == (Strings.LCase(name) ?? ""))
                    {
                        return i;
                    }
                }
            }
            else if (type == Thing.Object)
            {
                for (int i = 1, loopTo1 = _numberObjs; i <= loopTo1; i++)
                {
                    if (!string.IsNullOrEmpty(room) & (Strings.LCase(_objs[i].ObjectName) ?? "") == (Strings.LCase(name) ?? "") & (Strings.LCase(_objs[i].ContainerRoom) ?? "") == (Strings.LCase(room) ?? "") | string.IsNullOrEmpty(room) & (Strings.LCase(_objs[i].ObjectName) ?? "") == (Strings.LCase(name) ?? ""))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private DefineBlock GetThingBlock(string name, string room, Thing @type)
        {
            // Returns position where specified char/obj is defined in ASL code

            var result = new DefineBlock();

            if (type == Thing.Character)
            {
                for (int i = 1, loopTo = _numberChars; i <= loopTo; i++)
                {
                    if ((Strings.LCase(_chars[i].ObjectName) ?? "") == (Strings.LCase(name) ?? "") & (Strings.LCase(_chars[i].ContainerRoom) ?? "") == (Strings.LCase(room) ?? ""))
                    {
                        result.StartLine = _chars[i].DefinitionSectionStart;
                        result.EndLine = _chars[i].DefinitionSectionEnd;
                        return result;
                    }
                }
            }
            else if (type == Thing.Object)
            {
                for (int i = 1, loopTo1 = _numberObjs; i <= loopTo1; i++)
                {
                    if ((Strings.LCase(_objs[i].ObjectName) ?? "") == (Strings.LCase(name) ?? "") & (Strings.LCase(_objs[i].ContainerRoom) ?? "") == (Strings.LCase(room) ?? ""))
                    {
                        result.StartLine = _objs[i].DefinitionSectionStart;
                        result.EndLine = _objs[i].DefinitionSectionEnd;
                        return result;
                    }
                }
            }

            result.StartLine = 0;
            result.EndLine = 0;
            return result;
        }

        private string MakeRestoreData()
        {
            var data = new System.Text.StringBuilder();
            ChangeType[] objectData = new ChangeType[1];
            ChangeType[] roomData = new ChangeType[1];
            var numObjectData = default(int);
            var numRoomData = default(int);

            // <<< FILE HEADER DATA >>>

            data.Append("QUEST300" + '\0' + GetOriginalFilenameForQSG() + '\0');

            // The start point for encrypted data is after the filename
            int start = data.Length + 1;

            data.Append(_currentRoom + '\0');

            // Organise Change Log

            for (int i = 1, loopTo = _gameChangeData.NumberChanges; i <= loopTo; i++)
            {
                if (BeginsWith(_gameChangeData.ChangeData[i].AppliesTo, "object "))
                {
                    numObjectData = numObjectData + 1;
                    Array.Resize(ref objectData, numObjectData + 1);
                    objectData[numObjectData] = new ChangeType();
                    objectData[numObjectData] = _gameChangeData.ChangeData[i];
                }
                else if (BeginsWith(_gameChangeData.ChangeData[i].AppliesTo, "room "))
                {
                    numRoomData = numRoomData + 1;
                    Array.Resize(ref roomData, numRoomData + 1);
                    roomData[numRoomData] = new ChangeType();
                    roomData[numRoomData] = _gameChangeData.ChangeData[i];
                }
            }

            // <<< OBJECT CREATE/CHANGE DATA >>>

            data.Append(Strings.Trim(Conversion.Str(numObjectData + _changeLogObjects.Changes.Count)) + '\0');

            for (int i = 1, loopTo1 = numObjectData; i <= loopTo1; i++)
                data.Append(GetEverythingAfter(objectData[i].AppliesTo, "object ") + '\0' + objectData[i].Change + '\0');

            foreach (string key in _changeLogObjects.Changes.Keys)
            {
                string appliesTo = Strings.Split(key, "#")[0];
                string changeData = _changeLogObjects.Changes[key];

                data.Append(appliesTo + '\0' + changeData + '\0');
            }

            // <<< OBJECT EXIST/VISIBLE/ROOM DATA >>>

            data.Append(Strings.Trim(Conversion.Str(_numberObjs)) + '\0');

            for (int i = 1, loopTo2 = _numberObjs; i <= loopTo2; i++)
            {
                string exists;
                string visible;

                if (_objs[i].Exists)
                {
                    exists = "\u0001";
                }
                else
                {
                    exists = "\0";
                }

                if (_objs[i].Visible)
                {
                    visible = "\u0001";
                }
                else
                {
                    visible = "\0";
                }

                data.Append(_objs[i].ObjectName + '\0' + exists + visible + _objs[i].ContainerRoom + '\0');
            }

            // <<< ROOM CREATE/CHANGE DATA >>>

            data.Append(Strings.Trim(Conversion.Str(numRoomData)) + '\0');

            for (int i = 1, loopTo3 = numRoomData; i <= loopTo3; i++)
                data.Append(GetEverythingAfter(roomData[i].AppliesTo, "room ") + '\0' + roomData[i].Change + '\0');

            // <<< TIMER STATE DATA >>>

            data.Append(Strings.Trim(Conversion.Str(_numberTimers)) + '\0');

            for (int i = 1, loopTo4 = _numberTimers; i <= loopTo4; i++)
            {
                var t = _timers[i];
                data.Append(t.TimerName + '\0');

                if (t.TimerActive)
                {
                    data.Append('\u0001');
                }
                else
                {
                    data.Append('\0');
                }

                data.Append(Strings.Trim(Conversion.Str(t.TimerInterval)) + '\0');
                data.Append(Strings.Trim(Conversion.Str(t.TimerTicks)) + '\0');
            }

            // <<< STRING VARIABLE DATA >>>

            data.Append(Strings.Trim(Conversion.Str(_numberStringVariables)) + '\0');

            for (int i = 1, loopTo5 = _numberStringVariables; i <= loopTo5; i++)
            {
                var s = _stringVariable[i];
                data.Append(s.VariableName + '\0' + Strings.Trim(Conversion.Str(s.VariableUBound)) + '\0');

                for (int j = 0, loopTo6 = s.VariableUBound; j <= loopTo6; j++)
                    data.Append(s.VariableContents[j] + '\0');
            }

            // <<< NUMERIC VARIABLE DATA >>>

            data.Append(Strings.Trim(Conversion.Str(_numberNumericVariables)) + '\0');

            for (int i = 1, loopTo7 = _numberNumericVariables; i <= loopTo7; i++)
            {
                var n = _numericVariable[i];
                data.Append(n.VariableName + '\0' + Strings.Trim(Conversion.Str(n.VariableUBound)) + '\0');

                for (int j = 0, loopTo8 = n.VariableUBound; j <= loopTo8; j++)
                    data.Append(n.VariableContents[j] + '\0');
            }

            // Now encrypt data
            string dataString;
            var newFileData = new System.Text.StringBuilder();

            dataString = data.ToString();

            newFileData.Append(Strings.Left(dataString, start - 1));

            for (int i = start, loopTo9 = Strings.Len(dataString); i <= loopTo9; i++)
                newFileData.Append(Strings.Chr(255 - Strings.Asc(Strings.Mid(dataString, i, 1))));

            return newFileData.ToString();
        }

        private void MoveThing(string name, string room, Thing @type, Context ctx)
        {
            string oldRoom = "";

            int id = GetThingNumber(name, "", type);

            if (Strings.InStr(room, "[") > 0)
            {
                var idx = GetArrayIndex(room, ctx);
                room = room + Strings.Trim(Conversion.Str(idx.Index));
            }

            if (type == Thing.Character)
            {
                _chars[id].ContainerRoom = room;
            }
            else if (type == Thing.Object)
            {
                oldRoom = _objs[id].ContainerRoom;
                _objs[id].ContainerRoom = room;
            }

            if (_gameAslVersion >= 391)
            {
                // If this object contains other objects, move them too
                for (int i = 1, loopTo = _numberObjs; i <= loopTo; i++)
                {
                    if ((Strings.LCase(GetObjectProperty("parent", i, false, false)) ?? "") == (Strings.LCase(name) ?? ""))
                    {
                        MoveThing(_objs[i].ObjectName, room, type, ctx);
                    }
                }
            }

            UpdateObjectList(ctx);

            if (BeginsWith(Strings.LCase(room), "inventory") | BeginsWith(Strings.LCase(oldRoom), "inventory"))
            {
                UpdateItems(ctx);
            }
        }

        public void Pause(int duration)
        {
            _player.DoPause(duration);
            ChangeState(State.Waiting);

            lock (_waitLock)
                System.Threading.Monitor.Wait(_waitLock);
        }

        private string ConvertParameter(string parameter, string convertChar, ConvertType action, Context ctx)
        {
            // Returns a string with functions, string and
            // numeric variables executed or converted as
            // appropriate, read for display/etc.

            string result = "";
            int pos = 1;
            bool finished = false;

            do
            {
                int varPos = Strings.InStr(pos, parameter, convertChar);
                if (varPos == 0)
                {
                    varPos = Strings.Len(parameter) + 1;
                    finished = true;
                }

                string currentBit = Strings.Mid(parameter, pos, varPos - pos);
                result = result + currentBit;

                if (finished == false)
                {
                    int nextPos = Strings.InStr(varPos + 1, parameter, convertChar);

                    if (nextPos == 0)
                    {
                        LogASLError("Line parameter <" + parameter + "> has missing " + convertChar, LogType.WarningError);
                        return "<ERROR>";
                    }

                    string varName = Strings.Mid(parameter, varPos + 1, nextPos - 1 - varPos);

                    if (string.IsNullOrEmpty(varName))
                    {
                        result = result + convertChar;
                    }

                    else if (action == ConvertType.Strings)
                    {
                        result = result + GetStringContents(varName, ctx);
                    }
                    else if (action == ConvertType.Functions)
                    {
                        varName = EvaluateInlineExpressions(varName);
                        result = result + DoFunction(varName, ctx);
                    }
                    else if (action == ConvertType.Numeric)
                    {
                        result = result + Strings.Trim(Conversion.Str(GetNumericContents(varName, ctx)));
                    }
                    else if (action == ConvertType.Collectables)
                    {
                        result = result + Strings.Trim(Conversion.Str(GetCollectableAmount(varName)));
                    }

                    pos = nextPos + 1;
                }
            }
            while (!finished);

            return result;
        }

        private string DoFunction(string data, Context ctx)
        {
            string name, parameter;
            string intFuncResult = "";
            bool intFuncExecuted = false;
            int paramPos = Strings.InStr(data, "(");

            if (paramPos != 0)
            {
                name = Strings.Trim(Strings.Left(data, paramPos - 1));
                int endParamPos = Strings.InStrRev(data, ")");
                if (endParamPos == 0)
                {
                    LogASLError("Expected ) in $" + data + "$", LogType.WarningError);
                    return "";
                }
                parameter = Strings.Mid(data, paramPos + 1, endParamPos - paramPos - 1);
            }
            else
            {
                name = data;
                parameter = "";
            }

            DefineBlock block;
            block = DefineBlockParam("function", name);

            if (block.StartLine == 0 & block.EndLine == 0)
            {
                // Function does not exist; try an internal function.
                intFuncResult = DoInternalFunction(name, parameter, ctx);
                if (intFuncResult == "__NOTDEFINED")
                {
                    LogASLError("No such function '" + name + "'", LogType.WarningError);
                    return "[ERROR]";
                }
                else
                {
                    intFuncExecuted = true;
                }
            }

            if (intFuncExecuted)
            {
                return intFuncResult;
            }
            else
            {
                var newCtx = CopyContext(ctx);
                int numParameters = 0;
                int pos = 1;

                if (!string.IsNullOrEmpty(parameter))
                {
                    parameter = parameter + ";";
                    do
                    {
                        numParameters = numParameters + 1;
                        int scp = Strings.InStr(pos, parameter, ";");

                        string parameterData = Strings.Trim(Strings.Mid(parameter, pos, scp - pos));
                        SetStringContents("quest.function.parameter." + Strings.Trim(Conversion.Str(numParameters)), parameterData, ctx);

                        newCtx.NumParameters = numParameters;
                        Array.Resize(ref newCtx.Parameters, numParameters + 1);
                        newCtx.Parameters[numParameters] = parameterData;

                        pos = scp + 1;
                    }
                    while (pos < Strings.Len(parameter));
                    SetStringContents("quest.function.numparameters", Strings.Trim(Conversion.Str(numParameters)), ctx);
                }
                else
                {
                    SetStringContents("quest.function.numparameters", "0", ctx);
                    newCtx.NumParameters = 0;
                }

                string result = "";

                for (int i = block.StartLine + 1, loopTo = block.EndLine - 1; i <= loopTo; i++)
                {
                    ExecuteScript(_lines[i], newCtx);
                    result = newCtx.FunctionReturnValue;
                }

                return result;
            }
        }

        private string DoInternalFunction(string name, string parameter, Context ctx)
        {
            var parameters = default(string[]);
            var untrimmedParameters = default(string[]);
            var objId = default(int);
            int numParameters = 0;
            int pos = 1;

            if (!string.IsNullOrEmpty(parameter))
            {
                parameter = parameter + ";";
                do
                {
                    numParameters = numParameters + 1;
                    int scp = Strings.InStr(pos, parameter, ";");
                    Array.Resize(ref parameters, numParameters + 1);
                    Array.Resize(ref untrimmedParameters, numParameters + 1);

                    untrimmedParameters[numParameters] = Strings.Mid(parameter, pos, scp - pos);
                    parameters[numParameters] = Strings.Trim(untrimmedParameters[numParameters]);

                    pos = scp + 1;
                }
                while (pos < Strings.Len(parameter));

                // Remove final ";"
                parameter = Strings.Left(parameter, Strings.Len(parameter) - 1);
            }
            else
            {
                numParameters = 1;
                parameters = new string[2];
                untrimmedParameters = new string[2];
                parameters[1] = "";
                untrimmedParameters[1] = "";
            }

            string param2;
            string param3;

            if (name == "displayname")
            {
                objId = GetObjectId(parameters[1], ctx);
                if (objId == -1)
                {
                    LogASLError("Object '" + parameters[1] + "' does not exist", LogType.WarningError);
                    return "!";
                }
                else
                {
                    return _objs[GetObjectId(parameters[1], ctx)].ObjectAlias;
                }
            }
            else if (name == "numberparameters")
            {
                return Strings.Trim(Conversion.Str(ctx.NumParameters));
            }
            else if (name == "parameter")
            {
                if (numParameters == 0)
                {
                    LogASLError("No parameter number specified for $parameter$ function", LogType.WarningError);
                    return "";
                }
                else if (Conversion.Val(parameters[1]) > ctx.NumParameters)
                {
                    LogASLError("No parameter number " + parameters[1] + " sent to this function", LogType.WarningError);
                    return "";
                }
                else
                {
                    return Strings.Trim(ctx.Parameters[Conversions.ToInteger(parameters[1])]);
                }
            }
            else if (name == "gettag")
            {
                // Deprecated
                return FindStatement(DefineBlockParam("room", parameters[1]), parameters[2]);
            }
            else if (name == "objectname")
            {
                return _objs[ctx.CallingObjectId].ObjectName;
            }
            else if (name == "locationof")
            {
                for (int i = 1, loopTo = _numberChars; i <= loopTo; i++)
                {
                    if ((Strings.LCase(_chars[i].ObjectName) ?? "") == (Strings.LCase(parameters[1]) ?? ""))
                    {
                        return _chars[i].ContainerRoom;
                    }
                }

                for (int i = 1, loopTo1 = _numberObjs; i <= loopTo1; i++)
                {
                    if ((Strings.LCase(_objs[i].ObjectName) ?? "") == (Strings.LCase(parameters[1]) ?? ""))
                    {
                        return _objs[i].ContainerRoom;
                    }
                }
            }
            else if (name == "lengthof")
            {
                return Conversion.Str(Strings.Len(untrimmedParameters[1]));
            }
            else if (name == "left")
            {
                if (Conversion.Val(parameters[2]) < 0d)
                {
                    LogASLError("Invalid function call in '$Left$(" + parameters[1] + "; " + parameters[2] + ")$'", LogType.WarningError);
                    return "!";
                }
                else
                {
                    return Strings.Left(parameters[1], Conversions.ToInteger(parameters[2]));
                }
            }
            else if (name == "right")
            {
                if (Conversion.Val(parameters[2]) < 0d)
                {
                    LogASLError("Invalid function call in '$Right$(" + parameters[1] + "; " + parameters[2] + ")$'", LogType.WarningError);
                    return "!";
                }
                else
                {
                    return Strings.Right(parameters[1], Conversions.ToInteger(parameters[2]));
                }
            }
            else if (name == "mid")
            {
                if (numParameters == 3)
                {
                    if (Conversion.Val(parameters[2]) < 0d)
                    {
                        LogASLError("Invalid function call in '$Mid$(" + parameters[1] + "; " + parameters[2] + "; " + parameters[3] + ")$'", LogType.WarningError);
                        return "!";
                    }
                    else
                    {
                        return Strings.Mid(parameters[1], Conversions.ToInteger(parameters[2]), Conversions.ToInteger(parameters[3]));
                    }
                }
                else if (numParameters == 2)
                {
                    if (Conversion.Val(parameters[2]) < 0d)
                    {
                        LogASLError("Invalid function call in '$Mid$(" + parameters[1] + "; " + parameters[2] + ")$'", LogType.WarningError);
                        return "!";
                    }
                    else
                    {
                        return Strings.Mid(parameters[1], Conversions.ToInteger(parameters[2]));
                    }
                }
                LogASLError("Invalid function call to '$Mid$(...)$'", LogType.WarningError);
                return "";
            }
            else if (name == "rand")
            {
                return Conversion.Str(Conversion.Int(_random.NextDouble() * (Conversions.ToDouble(parameters[2]) - Conversions.ToDouble(parameters[1]) + 1d)) + Conversions.ToDouble(parameters[1]));
            }
            else if (name == "instr")
            {
                if (numParameters == 3)
                {
                    param3 = "";
                    if (Strings.InStr(parameters[3], "_") != 0)
                    {
                        for (int i = 1, loopTo2 = Strings.Len(parameters[3]); i <= loopTo2; i++)
                        {
                            if (Strings.Mid(parameters[3], i, 1) == "_")
                            {
                                param3 = param3 + " ";
                            }
                            else
                            {
                                param3 = param3 + Strings.Mid(parameters[3], i, 1);
                            }
                        }
                    }
                    else
                    {
                        param3 = parameters[3];
                    }
                    if (Conversion.Val(parameters[1]) <= 0d)
                    {
                        LogASLError("Invalid function call in '$instr(" + parameters[1] + "; " + parameters[2] + "; " + parameters[3] + ")$'", LogType.WarningError);
                        return "!";
                    }
                    else
                    {
                        return Strings.Trim(Conversion.Str(Strings.InStr(Conversions.ToInteger(parameters[1]), parameters[2], param3)));
                    }
                }
                else if (numParameters == 2)
                {
                    param2 = "";
                    if (Strings.InStr(parameters[2], "_") != 0)
                    {
                        for (int i = 1, loopTo3 = Strings.Len(parameters[2]); i <= loopTo3; i++)
                        {
                            if (Strings.Mid(parameters[2], i, 1) == "_")
                            {
                                param2 = param2 + " ";
                            }
                            else
                            {
                                param2 = param2 + Strings.Mid(parameters[2], i, 1);
                            }
                        }
                    }
                    else
                    {
                        param2 = parameters[2];
                    }
                    return Strings.Trim(Conversion.Str(Strings.InStr(parameters[1], param2)));
                }
                LogASLError("Invalid function call to '$Instr$(...)$'", LogType.WarningError);
                return "";
            }
            else if (name == "ucase")
            {
                return Strings.UCase(parameters[1]);
            }
            else if (name == "lcase")
            {
                return Strings.LCase(parameters[1]);
            }
            else if (name == "capfirst")
            {
                return Strings.UCase(Strings.Left(parameters[1], 1)) + Strings.Mid(parameters[1], 2);
            }
            else if (name == "symbol")
            {
                if (parameters[1] == "lt")
                {
                    return "<";
                }
                else if (parameters[1] == "gt")
                {
                    return ">";
                }
                else
                {
                    return "!";
                }
            }
            else if (name == "loadmethod")
            {
                return _gameLoadMethod;
            }
            else if (name == "timerstate")
            {
                for (int i = 1, loopTo4 = _numberTimers; i <= loopTo4; i++)
                {
                    if ((Strings.LCase(_timers[i].TimerName) ?? "") == (Strings.LCase(parameters[1]) ?? ""))
                    {
                        if (_timers[i].TimerActive)
                        {
                            return "1";
                        }
                        else
                        {
                            return "0";
                        }
                    }
                }
                LogASLError("No such timer '" + parameters[1] + "'", LogType.WarningError);
                return "!";
            }
            else if (name == "timerinterval")
            {
                for (int i = 1, loopTo5 = _numberTimers; i <= loopTo5; i++)
                {
                    if ((Strings.LCase(_timers[i].TimerName) ?? "") == (Strings.LCase(parameters[1]) ?? ""))
                    {
                        return Conversion.Str(_timers[i].TimerInterval);
                    }
                }
                LogASLError("No such timer '" + parameters[1] + "'", LogType.WarningError);
                return "!";
            }
            else if (name == "ubound")
            {
                for (int i = 1, loopTo6 = _numberNumericVariables; i <= loopTo6; i++)
                {
                    if ((Strings.LCase(_numericVariable[i].VariableName) ?? "") == (Strings.LCase(parameters[1]) ?? ""))
                    {
                        return Strings.Trim(Conversion.Str(_numericVariable[i].VariableUBound));
                    }
                }

                for (int i = 1, loopTo7 = _numberStringVariables; i <= loopTo7; i++)
                {
                    if ((Strings.LCase(_stringVariable[i].VariableName) ?? "") == (Strings.LCase(parameters[1]) ?? ""))
                    {
                        return Strings.Trim(Conversion.Str(_stringVariable[i].VariableUBound));
                    }
                }

                LogASLError("No such variable '" + parameters[1] + "'", LogType.WarningError);
                return "!";
            }
            else if (name == "objectproperty")
            {
                bool FoundObj = false;
                for (int i = 1, loopTo8 = _numberObjs; i <= loopTo8; i++)
                {
                    if ((Strings.LCase(_objs[i].ObjectName) ?? "") == (Strings.LCase(parameters[1]) ?? ""))
                    {
                        FoundObj = true;
                        objId = i;
                        i = _numberObjs;
                    }
                }

                if (!FoundObj)
                {
                    LogASLError("No such object '" + parameters[1] + "'", LogType.WarningError);
                    return "!";
                }
                else
                {
                    return GetObjectProperty(parameters[2], objId);
                }
            }
            else if (name == "getobjectname")
            {
                if (numParameters == 3)
                {
                    objId = Disambiguate(parameters[1], parameters[2] + ";" + parameters[3], ctx);
                }
                else if (numParameters == 2)
                {
                    objId = Disambiguate(parameters[1], parameters[2], ctx);
                }
                else
                {

                    objId = Disambiguate(parameters[1], _currentRoom + ";inventory", ctx);
                }

                if (objId <= -1)
                {
                    LogASLError("No object found with display name '" + parameters[1] + "'", LogType.WarningError);
                    return "!";
                }
                else
                {
                    return _objs[objId].ObjectName;
                }
            }
            else if (name == "thisobject")
            {
                return _objs[ctx.CallingObjectId].ObjectName;
            }
            else if (name == "thisobjectname")
            {
                return _objs[ctx.CallingObjectId].ObjectAlias;
            }
            else if (name == "speechenabled")
            {
                return "1";
            }
            else if (name == "removeformatting")
            {
                return RemoveFormatting(parameter);
            }
            else if (name == "findexit" & _gameAslVersion >= 410)
            {
                var e = FindExit(parameter);
                if (e is null)
                {
                    return "";
                }
                else
                {
                    return _objs[e.GetObjId()].ObjectName;
                }
            }

            return "__NOTDEFINED";
        }

        private void ExecFor(string line, Context ctx)
        {
            // See if this is a "for each" loop:
            if (BeginsWith(line, "each "))
            {
                ExecForEach(GetEverythingAfter(line, "each "), ctx);
                return;
            }

            // Executes a for loop, of form:
            // for <variable; startvalue; endvalue> script
            int endValue;
            int stepValue;
            string forData = GetParameter(line, ctx);

            // Extract individual components:
            int scp1 = Strings.InStr(forData, ";");
            int scp2 = Strings.InStr(scp1 + 1, forData, ";");
            int scp3 = Strings.InStr(scp2 + 1, forData, ";");
            string counterVariable = Strings.Trim(Strings.Left(forData, scp1 - 1));
            int startValue = Conversions.ToInteger(Strings.Mid(forData, scp1 + 1, scp2 - 1 - scp1));

            if (scp3 != 0)
            {
                endValue = Conversions.ToInteger(Strings.Mid(forData, scp2 + 1, scp3 - 1 - scp2));
                stepValue = Conversions.ToInteger(Strings.Mid(forData, scp3 + 1));
            }
            else
            {
                endValue = Conversions.ToInteger(Strings.Mid(forData, scp2 + 1));
                stepValue = 1;
            }

            string loopScript = Strings.Trim(Strings.Mid(line, Strings.InStr(line, ">") + 1));

            for (double i = startValue, loopTo = endValue; (double)stepValue >= 0 ? i <= loopTo : i >= loopTo; i += stepValue)
            {
                SetNumericVariableContents(counterVariable, i, ctx);
                ExecuteScript(loopScript, ctx);
                i = GetNumericContents(counterVariable, ctx);
            }
        }

        private void ExecSetVar(string varInfo, Context ctx)
        {
            // Sets variable contents from a script parameter.
            // Eg <var1;7> sets numeric variable var1
            // to 7

            int scp = Strings.InStr(varInfo, ";");
            string varName = Strings.Trim(Strings.Left(varInfo, scp - 1));
            string varCont = Strings.Trim(Strings.Mid(varInfo, scp + 1));
            var idx = GetArrayIndex(varName, ctx);

            if (Information.IsNumeric(idx.Name))
            {
                LogASLError("Invalid numeric variable name '" + idx.Name + "' - variable names cannot be numeric", LogType.WarningError);
                return;
            }

            try
            {
                if (_gameAslVersion >= 391)
                {
                    var expResult = ExpressionHandler(varCont);
                    if (expResult.Success == ExpressionSuccess.OK)
                    {
                        varCont = expResult.Result;
                    }
                    else
                    {
                        varCont = "0";
                        LogASLError("Error setting numeric variable <" + varInfo + "> : " + expResult.Message, LogType.WarningError);
                    }
                }
                else
                {
                    string obscuredVarInfo = ObscureNumericExps(varCont);
                    int opPos = Strings.InStr(obscuredVarInfo, "+");
                    if (opPos == 0)
                        opPos = Strings.InStr(obscuredVarInfo, "*");
                    if (opPos == 0)
                        opPos = Strings.InStr(obscuredVarInfo, "/");
                    if (opPos == 0)
                        opPos = Strings.InStr(2, obscuredVarInfo, "-");

                    if (opPos != 0)
                    {
                        string op = Strings.Mid(varCont, opPos, 1);
                        double num1 = Conversion.Val(Strings.Left(varCont, opPos - 1));
                        double num2 = Conversion.Val(Strings.Mid(varCont, opPos + 1));

                        switch (op ?? "")
                        {
                            case "+":
                                {
                                    varCont = Conversion.Str(num1 + num2);
                                    break;
                                }
                            case "-":
                                {
                                    varCont = Conversion.Str(num1 - num2);
                                    break;
                                }
                            case "*":
                                {
                                    varCont = Conversion.Str(num1 * num2);
                                    break;
                                }
                            case "/":
                                {
                                    if (num2 != 0d)
                                    {
                                        varCont = Conversion.Str(num1 / num2);
                                    }
                                    else
                                    {
                                        LogASLError("Division by zero - The result of this operation has been set to zero.", LogType.WarningError);
                                        varCont = "0";
                                    }

                                    break;
                                }
                        }
                    }
                }

                SetNumericVariableContents(idx.Name, Conversion.Val(varCont), ctx, idx.Index);
            }
            catch
            {
                LogASLError("Error setting variable '" + idx.Name + "' to '" + varCont + "'", LogType.WarningError);
            }
        }

        private bool ExecuteIfAsk(string question)
        {
            _player.ShowQuestion(question);
            ChangeState(State.Waiting);

            lock (_waitLock)
                System.Threading.Monitor.Wait(_waitLock);

            return _questionResponse;
        }

        private void SetQuestionResponse(bool response)
        {
            var runnerThread = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(SetQuestionResponseInNewThread));
            ChangeState(State.Working);
            runnerThread.Start(response);
            WaitForStateChange(State.Working);
        }

        void IASL.SetQuestionResponse(bool response) => SetQuestionResponse(response);

        private void SetQuestionResponseInNewThread(object response)
        {
            _questionResponse = (bool)response;

            lock (_waitLock)
                System.Threading.Monitor.PulseAll(_waitLock);
        }

        private bool ExecuteIfGot(string item)
        {
            if (_gameAslVersion >= 280)
            {
                for (int i = 1, loopTo = _numberObjs; i <= loopTo; i++)
                {
                    if ((Strings.LCase(_objs[i].ObjectName) ?? "") == (Strings.LCase(item) ?? ""))
                    {
                        return _objs[i].ContainerRoom == "inventory" & _objs[i].Exists;
                    }
                }

                LogASLError("No object '" + item + "' defined.", LogType.WarningError);
                return false;
            }

            for (int i = 1, loopTo1 = _numberItems; i <= loopTo1; i++)
            {
                if ((Strings.LCase(_items[i].Name) ?? "") == (Strings.LCase(item) ?? ""))
                {
                    return _items[i].Got;
                }
            }

            LogASLError("Item '" + item + "' not defined.", LogType.WarningError);
            return false;
        }

        private bool ExecuteIfHas(string condition)
        {
            double checkValue;
            var colNum = default(int);
            int scp = Strings.InStr(condition, ";");
            string name = Strings.Trim(Strings.Left(condition, scp - 1));
            string newVal = Strings.Trim(Strings.Right(condition, Strings.Len(condition) - scp));
            bool found = false;

            for (int i = 1, loopTo = _numCollectables; i <= loopTo; i++)
            {
                if ((_collectables[i].Name ?? "") == (name ?? ""))
                {
                    colNum = i;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                LogASLError("No such collectable in " + condition, LogType.WarningError);
                return false;
            }

            string op = Strings.Left(newVal, 1);
            string newValue = Strings.Trim(Strings.Right(newVal, Strings.Len(newVal) - 1));
            if (Information.IsNumeric(newValue))
            {
                checkValue = Conversion.Val(newValue);
            }
            else
            {
                checkValue = GetCollectableAmount(newValue);
            }

            if (op == "+")
            {
                return _collectables[colNum].Value > checkValue;
            }
            else if (op == "-")
            {
                return _collectables[colNum].Value < checkValue;
            }
            else if (op == "=")
            {
                return _collectables[colNum].Value == checkValue;
            }

            return false;
        }

        private bool ExecuteIfIs(string condition)
        {
            string value1, value2;
            string op;
            var expectNumerics = default(bool);
            ExpressionResult expResult;

            int scp = Strings.InStr(condition, ";");
            if (scp == 0)
            {
                LogASLError("Expected second parameter in 'is " + condition + "'", LogType.WarningError);
                return false;
            }

            int scp2 = Strings.InStr(scp + 1, condition, ";");
            if (scp2 == 0)
            {
                // Only two parameters => standard "="
                op = "=";
                value1 = Strings.Trim(Strings.Left(condition, scp - 1));
                value2 = Strings.Trim(Strings.Mid(condition, scp + 1));
            }
            else
            {
                value1 = Strings.Trim(Strings.Left(condition, scp - 1));
                op = Strings.Trim(Strings.Mid(condition, scp + 1, scp2 - scp - 1));
                value2 = Strings.Trim(Strings.Mid(condition, scp2 + 1));
            }

            if (_gameAslVersion >= 391)
            {
                // Evaluate expressions in Value1 and Value2
                expResult = ExpressionHandler(value1);

                if (expResult.Success == ExpressionSuccess.OK)
                {
                    value1 = expResult.Result;
                }

                expResult = ExpressionHandler(value2);

                if (expResult.Success == ExpressionSuccess.OK)
                {
                    value2 = expResult.Result;
                }
            }

            bool result = false;

            switch (op ?? "")
            {
                case "=":
                    {
                        if ((Strings.LCase(value1) ?? "") == (Strings.LCase(value2) ?? ""))
                        {
                            result = true;
                        }
                        expectNumerics = false;
                        break;
                    }
                case "!=":
                    {
                        if ((Strings.LCase(value1) ?? "") != (Strings.LCase(value2) ?? ""))
                        {
                            result = true;
                        }
                        expectNumerics = false;
                        break;
                    }
                case "gt":
                    {
                        if (Conversion.Val(value1) > Conversion.Val(value2))
                        {
                            result = true;
                        }
                        expectNumerics = true;
                        break;
                    }
                case "lt":
                    {
                        if (Conversion.Val(value1) < Conversion.Val(value2))
                        {
                            result = true;
                        }
                        expectNumerics = true;
                        break;
                    }
                case "gt=":
                    {
                        if (Conversion.Val(value1) >= Conversion.Val(value2))
                        {
                            result = true;
                        }
                        expectNumerics = true;
                        break;
                    }
                case "lt=":
                    {
                        if (Conversion.Val(value1) <= Conversion.Val(value2))
                        {
                            result = true;
                        }
                        expectNumerics = true;
                        break;
                    }

                default:
                    {
                        LogASLError("Unrecognised comparison condition in 'is " + condition + "'", LogType.WarningError);
                        break;
                    }
            }

            if (expectNumerics)
            {
                if (!(Information.IsNumeric(value1) & Information.IsNumeric(value2)))
                {
                    LogASLError("Expected numeric comparison comparing '" + value1 + "' and '" + value2 + "'", LogType.WarningError);
                }
            }

            return result;
        }

        private double GetNumericContents(string name, Context ctx, bool noError = false)
        {
            var numNumber = default(int);
            int arrayIndex;
            bool exists = false;

            // First, see if the variable already exists. If it
            // does, get its contents. If not, generate an error.

            if (Strings.InStr(name, "[") != 0 & Strings.InStr(name, "]") != 0)
            {
                int op = Strings.InStr(name, "[");
                int cp = Strings.InStr(name, "]");
                string arrayIndexData = Strings.Mid(name, op + 1, cp - op - 1);
                if (Information.IsNumeric(arrayIndexData))
                {
                    arrayIndex = Conversions.ToInteger(arrayIndexData);
                }
                else
                {
                    arrayIndex = (int)Math.Round(GetNumericContents(arrayIndexData, ctx));
                }
                name = Strings.Left(name, op - 1);
            }
            else
            {
                arrayIndex = 0;
            }

            if (_numberNumericVariables > 0)
            {
                for (int i = 1, loopTo = _numberNumericVariables; i <= loopTo; i++)
                {
                    if ((Strings.LCase(_numericVariable[i].VariableName) ?? "") == (Strings.LCase(name) ?? ""))
                    {
                        numNumber = i;
                        exists = true;
                        break;
                    }
                }
            }

            if (!exists)
            {
                if (!noError)
                    LogASLError("No numeric variable '" + name + "' defined.", LogType.WarningError);
                return -32767;
            }

            if (arrayIndex > _numericVariable[numNumber].VariableUBound)
            {
                if (!noError)
                    LogASLError("Array index of '" + name + "[" + Strings.Trim(Conversion.Str(arrayIndex)) + "]' too big.", LogType.WarningError);
                return -32766;
            }

            // Now, set the contents
            return Conversion.Val(_numericVariable[numNumber].VariableContents[arrayIndex]);
        }

        internal void PlayerErrorMessage(PlayerError e, Context ctx)
        {
            Print(GetErrorMessage(e, ctx), ctx);
        }

        private void PlayerErrorMessage_ExtendInfo(PlayerError e, Context ctx, string extraInfo)
        {
            string message = GetErrorMessage(e, ctx);

            if (!string.IsNullOrEmpty(extraInfo))
            {
                if (Strings.Right(message, 1) == ".")
                {
                    message = Strings.Left(message, Strings.Len(message) - 1);
                }

                message = message + " - " + extraInfo + ".";
            }

            Print(message, ctx);
        }

        private string GetErrorMessage(PlayerError e, Context ctx)
        {
            return ConvertParameter(ConvertParameter(ConvertParameter(_playerErrorMessageString[(int)e], "%", ConvertType.Numeric, ctx), "$", ConvertType.Functions, ctx), "#", ConvertType.Strings, ctx);
        }

        private void PlayMedia(string filename)
        {
            PlayMedia(filename, false, false);
        }

        private void PlayMedia(string filename, bool sync, bool looped)
        {
            if (filename.Length == 0)
            {
                _player.StopSound();
            }
            else
            {
                if (looped & sync)
                    sync = false; // Can't loop and sync at the same time, that would just hang!

                _player.PlaySound(filename, sync, looped);

                if (sync)
                {
                    ChangeState(State.Waiting);
                }

                if (sync)
                {
                    lock (_waitLock)
                        System.Threading.Monitor.Wait(_waitLock);
                }
            }
        }

        private void PlayWav(string parameter)
        {
            bool sync = false;
            bool looped = false;
            var @params = new List<string>(parameter.Split(';'));

            @params = new List<string>(@params.Select((p) => Strings.Trim(p)));

            string filename = @params[0];

            if (@params.Contains("loop"))
                looped = true;
            if (@params.Contains("sync"))
                sync = true;

            if (filename.Length > 0 & Strings.InStr(filename, ".") == 0)
            {
                filename = filename + ".wav";
            }

            PlayMedia(filename, sync, looped);
        }

        private void RestoreGameData(string fileData)
        {
            string appliesTo;
            string data = "";
            int objId, timerNum = default;
            int varUbound;
            bool found;
            var numStoredData = default(int);
            ChangeType[] storedData = new ChangeType[1];
            var decryptedFile = new System.Text.StringBuilder();

            // Decrypt file
            for (int i = 1, loopTo = Strings.Len(fileData); i <= loopTo; i++)
                decryptedFile.Append(Strings.Chr(255 - Strings.Asc(Strings.Mid(fileData, i, 1))));

            _fileData = decryptedFile.ToString();
            _currentRoom = GetNextChunk();

            // OBJECTS

            int numData = Conversions.ToInteger(GetNextChunk());
            var createdObjects = new List<string>();

            for (int i = 1, loopTo1 = numData; i <= loopTo1; i++)
            {
                appliesTo = GetNextChunk();
                data = GetNextChunk();

                // As of Quest 4.0, properties and actions are put into StoredData while we load the file,
                // and then processed later. This is so any created rooms pick up their properties - otherwise
                // we try to set them before they've been created.

                if (BeginsWith(data, "properties ") | BeginsWith(data, "action "))
                {
                    numStoredData = numStoredData + 1;
                    Array.Resize(ref storedData, numStoredData + 1);
                    storedData[numStoredData] = new ChangeType();
                    storedData[numStoredData].AppliesTo = appliesTo;
                    storedData[numStoredData].Change = data;
                }
                else if (BeginsWith(data, "create "))
                {
                    string createData = appliesTo + ";" + GetEverythingAfter(data, "create ");
                    // workaround bug where duplicate "create" entries appear in the restore data
                    if (!createdObjects.Contains(createData))
                    {
                        ExecuteCreate("object <" + createData + ">", _nullContext);
                        createdObjects.Add(createData);
                    }
                }
                else
                {
                    LogASLError("QSG Error: Unrecognised item '" + appliesTo + "; " + data + "'", LogType.InternalError);
                }
            }

            numData = Conversions.ToInteger(GetNextChunk());
            for (int i = 1, loopTo2 = numData; i <= loopTo2; i++)
            {
                appliesTo = GetNextChunk();
                data = GetFileDataChars(2);
                objId = GetObjectIdNoAlias(appliesTo);

                if (Strings.Left(data, 1) == "\u0001")
                {
                    _objs[objId].Exists = true;
                }
                else
                {
                    _objs[objId].Exists = false;
                }

                if (Strings.Right(data, 1) == "\u0001")
                {
                    _objs[objId].Visible = true;
                }
                else
                {
                    _objs[objId].Visible = false;
                }

                _objs[objId].ContainerRoom = GetNextChunk();
            }

            // ROOMS

            numData = Conversions.ToInteger(GetNextChunk());

            for (int i = 1, loopTo3 = numData; i <= loopTo3; i++)
            {
                appliesTo = GetNextChunk();
                data = GetNextChunk();

                if (BeginsWith(data, "exit "))
                {
                    ExecuteCreate(data, _nullContext);
                }
                else if (data == "create")
                {
                    ExecuteCreate("room <" + appliesTo + ">", _nullContext);
                }
                else if (BeginsWith(data, "destroy exit "))
                {
                    DestroyExit(appliesTo + "; " + GetEverythingAfter(data, "destroy exit "), _nullContext);
                }
            }

            // Now go through and apply object properties and actions

            for (int i = 1, loopTo4 = numStoredData; i <= loopTo4; i++)
            {
                var d = storedData[i];
                if (BeginsWith(d.Change, "properties "))
                {
                    AddToObjectProperties(GetEverythingAfter(d.Change, "properties "), GetObjectIdNoAlias(d.AppliesTo), _nullContext);
                }
                else if (BeginsWith(d.Change, "action "))
                {
                    AddToObjectActions(GetEverythingAfter(d.Change, "action "), GetObjectIdNoAlias(d.AppliesTo), _nullContext);
                }
            }

            // TIMERS

            numData = Conversions.ToInteger(GetNextChunk());
            for (int i = 1, loopTo5 = numData; i <= loopTo5; i++)
            {
                found = false;
                appliesTo = GetNextChunk();
                for (int j = 1, loopTo6 = _numberTimers; j <= loopTo6; j++)
                {
                    if ((_timers[j].TimerName ?? "") == (appliesTo ?? ""))
                    {
                        timerNum = j;
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    var t = _timers[timerNum];
                    string thisChar = GetFileDataChars(1);

                    if (thisChar == "\u0001")
                    {
                        t.TimerActive = true;
                    }
                    else
                    {
                        t.TimerActive = false;
                    }

                    t.TimerInterval = Conversions.ToInteger(GetNextChunk());
                    t.TimerTicks = Conversions.ToInteger(GetNextChunk());
                }
            }

            // STRING VARIABLES

            // Set this flag so we don't run any status variable onchange scripts while restoring
            _gameIsRestoring = true;

            numData = Conversions.ToInteger(GetNextChunk());
            for (int i = 1, loopTo7 = numData; i <= loopTo7; i++)
            {
                appliesTo = GetNextChunk();
                varUbound = Conversions.ToInteger(GetNextChunk());

                if (varUbound == 0)
                {
                    data = GetNextChunk();
                    SetStringContents(appliesTo, data, _nullContext);
                }
                else
                {
                    for (int j = 0, loopTo8 = varUbound; j <= loopTo8; j++)
                    {
                        data = GetNextChunk();
                        SetStringContents(appliesTo, data, _nullContext, j);
                    }
                }
            }

            // NUMERIC VARIABLES

            numData = Conversions.ToInteger(GetNextChunk());
            for (int i = 1, loopTo9 = numData; i <= loopTo9; i++)
            {
                appliesTo = GetNextChunk();
                varUbound = Conversions.ToInteger(GetNextChunk());

                if (varUbound == 0)
                {
                    data = GetNextChunk();
                    SetNumericVariableContents(appliesTo, Conversion.Val(data), _nullContext);
                }
                else
                {
                    for (int j = 0, loopTo10 = varUbound; j <= loopTo10; j++)
                    {
                        data = GetNextChunk();
                        SetNumericVariableContents(appliesTo, Conversion.Val(data), _nullContext, j);
                    }
                }
            }

            _gameIsRestoring = false;
        }

        private void SetBackground(string col)
        {
            _player.SetBackground("#" + GetHTMLColour(col, "white"));
        }

        private void SetForeground(string col)
        {
            _player.SetForeground("#" + GetHTMLColour(col, "black"));
        }

        private void SetDefaultPlayerErrorMessages()
        {
            _playerErrorMessageString[(int)PlayerError.BadCommand] = "I don't understand your command. Type HELP for a list of valid commands.";
            _playerErrorMessageString[(int)PlayerError.BadGo] = "I don't understand your use of 'GO' - you must either GO in some direction, or GO TO a place.";
            _playerErrorMessageString[(int)PlayerError.BadGive] = "You didn't say who you wanted to give that to.";
            _playerErrorMessageString[(int)PlayerError.BadCharacter] = "I can't see anybody of that name here.";
            _playerErrorMessageString[(int)PlayerError.NoItem] = "You don't have that.";
            _playerErrorMessageString[(int)PlayerError.ItemUnwanted] = "#quest.error.gender# doesn't want #quest.error.article#.";
            _playerErrorMessageString[(int)PlayerError.BadLook] = "You didn't say what you wanted to look at.";
            _playerErrorMessageString[(int)PlayerError.BadThing] = "I can't see that here.";
            _playerErrorMessageString[(int)PlayerError.DefaultLook] = "Nothing out of the ordinary.";
            _playerErrorMessageString[(int)PlayerError.DefaultSpeak] = "#quest.error.gender# says nothing.";
            _playerErrorMessageString[(int)PlayerError.BadItem] = "I can't see that anywhere.";
            _playerErrorMessageString[(int)PlayerError.DefaultTake] = "You pick #quest.error.article# up.";
            _playerErrorMessageString[(int)PlayerError.BadUse] = "You didn't say what you wanted to use that on.";
            _playerErrorMessageString[(int)PlayerError.DefaultUse] = "You can't use that here.";
            _playerErrorMessageString[(int)PlayerError.DefaultOut] = "There's nowhere you can go out to around here.";
            _playerErrorMessageString[(int)PlayerError.BadPlace] = "You can't go there.";
            _playerErrorMessageString[(int)PlayerError.DefaultExamine] = "Nothing out of the ordinary.";
            _playerErrorMessageString[(int)PlayerError.BadTake] = "You can't take #quest.error.article#.";
            _playerErrorMessageString[(int)PlayerError.CantDrop] = "You can't drop that here.";
            _playerErrorMessageString[(int)PlayerError.DefaultDrop] = "You drop #quest.error.article#.";
            _playerErrorMessageString[(int)PlayerError.BadDrop] = "You are not carrying such a thing.";
            _playerErrorMessageString[(int)PlayerError.BadPronoun] = "I don't know what '#quest.error.pronoun#' you are referring to.";
            _playerErrorMessageString[(int)PlayerError.BadExamine] = "You didn't say what you wanted to examine.";
            _playerErrorMessageString[(int)PlayerError.AlreadyOpen] = "It is already open.";
            _playerErrorMessageString[(int)PlayerError.AlreadyClosed] = "It is already closed.";
            _playerErrorMessageString[(int)PlayerError.CantOpen] = "You can't open that.";
            _playerErrorMessageString[(int)PlayerError.CantClose] = "You can't close that.";
            _playerErrorMessageString[(int)PlayerError.DefaultOpen] = "You open it.";
            _playerErrorMessageString[(int)PlayerError.DefaultClose] = "You close it.";
            _playerErrorMessageString[(int)PlayerError.BadPut] = "You didn't specify what you wanted to put #quest.error.article# on or in.";
            _playerErrorMessageString[(int)PlayerError.CantPut] = "You can't put that there.";
            _playerErrorMessageString[(int)PlayerError.DefaultPut] = "Done.";
            _playerErrorMessageString[(int)PlayerError.CantRemove] = "You can't remove that.";
            _playerErrorMessageString[(int)PlayerError.AlreadyPut] = "It is already there.";
            _playerErrorMessageString[(int)PlayerError.DefaultRemove] = "Done.";
            _playerErrorMessageString[(int)PlayerError.Locked] = "The exit is locked.";
            _playerErrorMessageString[(int)PlayerError.DefaultWait] = "Press a key to continue...";
            _playerErrorMessageString[(int)PlayerError.AlreadyTaken] = "You already have that.";
        }

        private void SetFont(string name)
        {
            if (string.IsNullOrEmpty(name))
                name = _defaultFontName;
            _player.SetFont(name);
        }

        private void SetFontSize(double size)
        {
            if (size == 0d)
                size = _defaultFontSize;
            _player.SetFontSize(size.ToString());
        }

        private void SetNumericVariableContents(string name, double content, Context ctx, int arrayIndex = 0)
        {
            var numNumber = default(int);
            bool exists = false;

            if (Information.IsNumeric(name))
            {
                LogASLError("Illegal numeric variable name '" + name + "' - check you didn't put % around the variable name in the ASL code", LogType.WarningError);
                return;
            }

            // First, see if variable already exists. If it does,
            // modify it. If not, create it.

            if (_numberNumericVariables > 0)
            {
                for (int i = 1, loopTo = _numberNumericVariables; i <= loopTo; i++)
                {
                    if ((Strings.LCase(_numericVariable[i].VariableName) ?? "") == (Strings.LCase(name) ?? ""))
                    {
                        numNumber = i;
                        exists = true;
                        break;
                    }
                }
            }

            if (exists == false)
            {
                _numberNumericVariables = _numberNumericVariables + 1;
                numNumber = _numberNumericVariables;
                Array.Resize(ref _numericVariable, numNumber + 1);
                _numericVariable[numNumber] = new VariableType();
                _numericVariable[numNumber].VariableUBound = arrayIndex;
            }

            if (arrayIndex > _numericVariable[numNumber].VariableUBound)
            {
                Array.Resize(ref _numericVariable[numNumber].VariableContents, arrayIndex + 1);
                _numericVariable[numNumber].VariableUBound = arrayIndex;
            }

            // Now, set the contents
            _numericVariable[numNumber].VariableName = name;
            Array.Resize(ref _numericVariable[numNumber].VariableContents, _numericVariable[numNumber].VariableUBound + 1);
            _numericVariable[numNumber].VariableContents[arrayIndex] = content.ToString();

            if (!string.IsNullOrEmpty(_numericVariable[numNumber].OnChangeScript) & !_gameIsRestoring)
            {
                string script = _numericVariable[numNumber].OnChangeScript;
                ExecuteScript(script, ctx);
            }

            if (!string.IsNullOrEmpty(_numericVariable[numNumber].DisplayString))
            {
                UpdateStatusVars(ctx);
            }
        }

        private void SetOpenClose(string name, bool open, Context ctx)
        {
            string cmd;

            if (open)
            {
                cmd = "open";
            }
            else
            {
                cmd = "close";
            }

            int id = GetObjectIdNoAlias(name);
            if (id == 0)
            {
                LogASLError("Invalid object name specified in '" + cmd + " <" + name + ">", LogType.WarningError);
                return;
            }

            DoOpenClose(id, open, false, ctx);
        }

        private void SetTimerState(string name, bool state)
        {
            for (int i = 1, loopTo = _numberTimers; i <= loopTo; i++)
            {
                if ((Strings.LCase(name) ?? "") == (Strings.LCase(_timers[i].TimerName) ?? ""))
                {
                    _timers[i].TimerActive = state;
                    _timers[i].BypassThisTurn = true;     // don't trigger timer during the turn it was first enabled
                    return;
                }
            }

            LogASLError("No such timer '" + name + "'", LogType.WarningError);
        }

        private SetResult SetUnknownVariableType(string variableData, Context ctx)
        {
            int scp = Strings.InStr(variableData, ";");
            if (scp == 0)
            {
                return SetResult.Error;
            }

            string name = Strings.Trim(Strings.Left(variableData, scp - 1));
            if (Strings.InStr(name, "[") != 0 & Strings.InStr(name, "]") != 0)
            {
                int pos = Strings.InStr(name, "[");
                name = Strings.Left(name, pos - 1);
            }

            string contents = Strings.Trim(Strings.Mid(variableData, scp + 1));

            for (int i = 1, loopTo = _numberStringVariables; i <= loopTo; i++)
            {
                if ((Strings.LCase(_stringVariable[i].VariableName) ?? "") == (Strings.LCase(name) ?? ""))
                {
                    ExecSetString(variableData, ctx);
                    return SetResult.Found;
                }
            }

            for (int i = 1, loopTo1 = _numberNumericVariables; i <= loopTo1; i++)
            {
                if ((Strings.LCase(_numericVariable[i].VariableName) ?? "") == (Strings.LCase(name) ?? ""))
                {
                    ExecSetVar(variableData, ctx);
                    return SetResult.Found;
                }
            }

            for (int i = 1, loopTo2 = _numCollectables; i <= loopTo2; i++)
            {
                if ((Strings.LCase(_collectables[i].Name) ?? "") == (Strings.LCase(name) ?? ""))
                {
                    ExecuteSetCollectable(variableData, ctx);
                    return SetResult.Found;
                }
            }

            return SetResult.Unfound;
        }

        private string SetUpChoiceForm(string blockName, Context ctx)
        {
            // Returns script to execute from choice block
            var block = DefineBlockParam("selection", blockName);
            string prompt = FindStatement(block, "info");

            var menuOptions = new Dictionary<string, string>();
            var menuScript = new Dictionary<string, string>();

            for (int i = block.StartLine + 1, loopTo = block.EndLine - 1; i <= loopTo; i++)
            {
                if (BeginsWith(_lines[i], "choice "))
                {
                    menuOptions.Add(i.ToString(), GetParameter(_lines[i], ctx));
                    menuScript.Add(i.ToString(), Strings.Trim(Strings.Right(_lines[i], Strings.Len(_lines[i]) - Strings.InStr(_lines[i], ">"))));
                }
            }

            Print("- |i" + prompt + "|xi", ctx);

            var mnu = new MenuData(prompt, menuOptions, false);
            string choice = ShowMenu(mnu);

            Print("- " + menuOptions[choice] + "|n", ctx);
            return menuScript[choice];
        }

        private void SetUpDefaultFonts()
        {
            // Sets up default fonts
            var gameblock = GetDefineBlock("game");

            _defaultFontName = "Arial";
            _defaultFontSize = 9d;

            for (int i = gameblock.StartLine + 1, loopTo = gameblock.EndLine - 1; i <= loopTo; i++)
            {
                if (BeginsWith(_lines[i], "default fontname "))
                {
                    string name = GetParameter(_lines[i], _nullContext);
                    if (!string.IsNullOrEmpty(name))
                    {
                        _defaultFontName = name;
                    }
                }
                else if (BeginsWith(_lines[i], "default fontsize "))
                {
                    int size = Conversions.ToInteger(GetParameter(_lines[i], _nullContext));
                    if (size != 0)
                    {
                        _defaultFontSize = size;
                    }
                }
            }
        }

        private void SetUpDisplayVariables()
        {
            for (int i = GetDefineBlock("game").StartLine + 1, loopTo = GetDefineBlock("game").EndLine - 1; i <= loopTo; i++)
            {
                if (BeginsWith(_lines[i], "define variable "))
                {

                    var variable = new VariableType();
                    variable.VariableContents = new string[1];

                    variable.VariableName = GetParameter(_lines[i], _nullContext);
                    variable.DisplayString = "";
                    variable.NoZeroDisplay = false;
                    variable.OnChangeScript = "";
                    variable.VariableContents[0] = "";
                    variable.VariableUBound = 0;

                    string @type = "numeric";

                    do
                    {
                        i = i + 1;

                        if (BeginsWith(_lines[i], "type "))
                        {
                            type = GetEverythingAfter(_lines[i], "type ");
                            if (type != "string" & type != "numeric")
                            {
                                LogASLError("Unrecognised variable type in variable '" + variable.VariableName + "' - type '" + type + "'", LogType.WarningError);
                                break;
                            }
                        }
                        else if (BeginsWith(_lines[i], "onchange "))
                        {
                            variable.OnChangeScript = GetEverythingAfter(_lines[i], "onchange ");
                        }
                        else if (BeginsWith(_lines[i], "display "))
                        {
                            string displayString = GetEverythingAfter(_lines[i], "display ");
                            if (BeginsWith(displayString, "nozero "))
                            {
                                variable.NoZeroDisplay = true;
                            }
                            variable.DisplayString = GetParameter(_lines[i], _nullContext, false);
                        }
                        else if (BeginsWith(_lines[i], "value "))
                        {
                            variable.VariableContents[0] = GetParameter(_lines[i], _nullContext);
                        }
                    }

                    while (Strings.Trim(_lines[i]) != "end define");

                    if (type == "string")
                    {
                        // Create string variable
                        _numberStringVariables = _numberStringVariables + 1;
                        int id = _numberStringVariables;
                        Array.Resize(ref _stringVariable, id + 1);
                        _stringVariable[id] = variable;
                        _numDisplayStrings = _numDisplayStrings + 1;
                    }
                    else if (type == "numeric")
                    {
                        if (string.IsNullOrEmpty(variable.VariableContents[0]))
                            variable.VariableContents[0] = 0.ToString();
                        _numberNumericVariables = _numberNumericVariables + 1;
                        int iNumNumber = _numberNumericVariables;
                        Array.Resize(ref _numericVariable, iNumNumber + 1);
                        _numericVariable[iNumNumber] = variable;
                        _numDisplayNumerics = _numDisplayNumerics + 1;
                    }
                }
            }

        }

        private void SetUpGameObject()
        {
            _numberObjs = 1;
            _objs = new ObjectType[2];
            _objs[1] = new ObjectType();
            var o = _objs[1];
            o.ObjectName = "game";
            o.ObjectAlias = "";
            o.Visible = false;
            o.Exists = true;

            int nestBlock = 0;
            for (int i = GetDefineBlock("game").StartLine + 1, loopTo = GetDefineBlock("game").EndLine - 1; i <= loopTo; i++)
            {
                if (nestBlock == 0)
                {
                    if (BeginsWith(_lines[i], "define "))
                    {
                        nestBlock = nestBlock + 1;
                    }
                    else if (BeginsWith(_lines[i], "properties "))
                    {
                        AddToObjectProperties(GetParameter(_lines[i], _nullContext), _numberObjs, _nullContext);
                    }
                    else if (BeginsWith(_lines[i], "type "))
                    {
                        o.NumberTypesIncluded = o.NumberTypesIncluded + 1;
                        Array.Resize(ref o.TypesIncluded, o.NumberTypesIncluded + 1);
                        o.TypesIncluded[o.NumberTypesIncluded] = GetParameter(_lines[i], _nullContext);

                        var propertyData = GetPropertiesInType(GetParameter(_lines[i], _nullContext));
                        AddToObjectProperties(propertyData.Properties, _numberObjs, _nullContext);
                        for (int k = 1, loopTo1 = propertyData.NumberActions; k <= loopTo1; k++)
                            AddObjectAction(_numberObjs, propertyData.Actions[k].ActionName, propertyData.Actions[k].Script);
                    }
                    else if (BeginsWith(_lines[i], "action "))
                    {
                        AddToObjectActions(GetEverythingAfter(_lines[i], "action "), _numberObjs, _nullContext);
                    }
                }
                else if (Strings.Trim(_lines[i]) == "end define")
                {
                    nestBlock = nestBlock - 1;
                }
            }
        }

        private void SetUpMenus()
        {
            bool exists = false;
            string menuTitle = "";
            var menuOptions = new Dictionary<string, string>();

            for (int i = 1, loopTo = _numberSections; i <= loopTo; i++)
            {
                if (BeginsWith(_lines[_defineBlocks[i].StartLine], "define menu "))
                {

                    if (exists)
                    {
                        LogASLError("Can't load menu '" + GetParameter(_lines[_defineBlocks[i].StartLine], _nullContext) + "' - only one menu can be added.", LogType.WarningError);
                    }
                    else
                    {
                        menuTitle = GetParameter(_lines[_defineBlocks[i].StartLine], _nullContext);

                        for (int j = _defineBlocks[i].StartLine + 1, loopTo1 = _defineBlocks[i].EndLine - 1; j <= loopTo1; j++)
                        {
                            if (!string.IsNullOrEmpty(Strings.Trim(_lines[j])))
                            {
                                int scp = Strings.InStr(_lines[j], ":");
                                if (scp == 0 & _lines[j] != "-")
                                {
                                    LogASLError("No menu command specified in menu '" + menuTitle + "', item '" + _lines[j], LogType.WarningError);
                                }
                                else if (_lines[j] == "-")
                                {
                                    menuOptions.Add("k" + j.ToString(), "-");
                                }
                                else
                                {
                                    menuOptions.Add(Strings.Trim(Strings.Mid(_lines[j], scp + 1)), Strings.Trim(Strings.Left(_lines[j], scp - 1)));

                                }
                            }
                        }

                        if (menuOptions.Count > 0)
                        {
                            exists = true;
                        }
                    }
                }
            }

            if (exists)
            {
                var windowMenu = new MenuData(menuTitle, menuOptions, false);
                _player.SetWindowMenu(windowMenu);
            }
        }

        private void SetUpOptions()
        {
            string opt;

            for (int i = GetDefineBlock("options").StartLine + 1, loopTo = GetDefineBlock("options").EndLine - 1; i <= loopTo; i++)
            {
                if (BeginsWith(_lines[i], "panes "))
                {
                    opt = Strings.LCase(Strings.Trim(GetEverythingAfter(_lines[i], "panes ")));
                    _player.SetPanesVisible(opt);
                }
                else if (BeginsWith(_lines[i], "abbreviations "))
                {
                    opt = Strings.LCase(Strings.Trim(GetEverythingAfter(_lines[i], "abbreviations ")));
                    if (opt == "off")
                        _useAbbreviations = false;
                    else
                        _useAbbreviations = true;
                }
            }
        }

        private void SetUpRoomData()
        {
            var defaultProperties = new PropertiesActions();

            // see if define type <defaultroom> exists:
            bool defaultExists = false;
            for (int i = 1, loopTo = _numberSections; i <= loopTo; i++)
            {
                if (Strings.Trim(_lines[_defineBlocks[i].StartLine]) == "define type <defaultroom>")
                {
                    defaultExists = true;
                    defaultProperties = GetPropertiesInType("defaultroom");
                    break;
                }
            }

            for (int i = 1, loopTo1 = _numberSections; i <= loopTo1; i++)
            {
                if (BeginsWith(_lines[_defineBlocks[i].StartLine], "define room "))
                {
                    _numberRooms = _numberRooms + 1;
                    Array.Resize(ref _rooms, _numberRooms + 1);
                    _rooms[_numberRooms] = new RoomType();

                    _numberObjs = _numberObjs + 1;
                    Array.Resize(ref _objs, _numberObjs + 1);
                    _objs[_numberObjs] = new ObjectType();

                    var r = _rooms[_numberRooms];

                    r.RoomName = GetParameter(_lines[_defineBlocks[i].StartLine], _nullContext);
                    _objs[_numberObjs].ObjectName = r.RoomName;
                    _objs[_numberObjs].IsRoom = true;
                    _objs[_numberObjs].CorresRoom = r.RoomName;
                    _objs[_numberObjs].CorresRoomId = _numberRooms;

                    r.ObjId = _numberObjs;

                    if (_gameAslVersion >= 410)
                    {
                        r.Exits = new LegacyASL.RoomExits(this);
                        r.Exits.SetObjId(r.ObjId);
                    }

                    if (defaultExists)
                    {
                        AddToObjectProperties(defaultProperties.Properties, _numberObjs, _nullContext);
                        for (int k = 1, loopTo2 = defaultProperties.NumberActions; k <= loopTo2; k++)
                            AddObjectAction(_numberObjs, defaultProperties.Actions[k].ActionName, defaultProperties.Actions[k].Script);
                    }

                    for (int j = _defineBlocks[i].StartLine + 1, loopTo3 = _defineBlocks[i].EndLine - 1; j <= loopTo3; j++)
                    {
                        if (BeginsWith(_lines[j], "define "))
                        {
                            // skip nested blocks
                            int nestedBlock = 1;
                            do
                            {
                                j = j + 1;
                                if (BeginsWith(_lines[j], "define "))
                                {
                                    nestedBlock = nestedBlock + 1;
                                }
                                else if (Strings.Trim(_lines[j]) == "end define")
                                {
                                    nestedBlock = nestedBlock - 1;
                                }
                            }
                            while (nestedBlock != 0);
                        }

                        if (_gameAslVersion >= 280 & BeginsWith(_lines[j], "alias "))
                        {
                            r.RoomAlias = GetParameter(_lines[j], _nullContext);
                            _objs[_numberObjs].ObjectAlias = r.RoomAlias;
                            if (_gameAslVersion >= 350)
                                AddToObjectProperties("alias=" + r.RoomAlias, _numberObjs, _nullContext);
                        }
                        else if (_gameAslVersion >= 280 & BeginsWith(_lines[j], "description "))
                        {
                            r.Description = GetTextOrScript(GetEverythingAfter(_lines[j], "description "));
                            if (_gameAslVersion >= 350)
                            {
                                if (r.Description.Type == TextActionType.Script)
                                {
                                    AddObjectAction(_numberObjs, "description", r.Description.Data);
                                }
                                else
                                {
                                    AddToObjectProperties("description=" + r.Description.Data, _numberObjs, _nullContext);
                                }
                            }
                        }
                        else if (BeginsWith(_lines[j], "out "))
                        {
                            r.Out.Text = GetParameter(_lines[j], _nullContext);
                            r.Out.Script = Strings.Trim(Strings.Mid(_lines[j], Strings.InStr(_lines[j], ">") + 1));
                            if (_gameAslVersion >= 350)
                            {
                                if (!string.IsNullOrEmpty(r.Out.Script))
                                {
                                    AddObjectAction(_numberObjs, "out", r.Out.Script);
                                }

                                AddToObjectProperties("out=" + r.Out.Text, _numberObjs, _nullContext);
                            }
                        }
                        else if (BeginsWith(_lines[j], "east "))
                        {
                            r.East = GetTextOrScript(GetEverythingAfter(_lines[j], "east "));
                            if (_gameAslVersion >= 350)
                            {
                                if (r.East.Type == TextActionType.Script)
                                {
                                    AddObjectAction(_numberObjs, "east", r.East.Data);
                                }
                                else
                                {
                                    AddToObjectProperties("east=" + r.East.Data, _numberObjs, _nullContext);
                                }
                            }
                        }
                        else if (BeginsWith(_lines[j], "west "))
                        {
                            r.West = GetTextOrScript(GetEverythingAfter(_lines[j], "west "));
                            if (_gameAslVersion >= 350)
                            {
                                if (r.West.Type == TextActionType.Script)
                                {
                                    AddObjectAction(_numberObjs, "west", r.West.Data);
                                }
                                else
                                {
                                    AddToObjectProperties("west=" + r.West.Data, _numberObjs, _nullContext);
                                }
                            }
                        }
                        else if (BeginsWith(_lines[j], "north "))
                        {
                            r.North = GetTextOrScript(GetEverythingAfter(_lines[j], "north "));
                            if (_gameAslVersion >= 350)
                            {
                                if (r.North.Type == TextActionType.Script)
                                {
                                    AddObjectAction(_numberObjs, "north", r.North.Data);
                                }
                                else
                                {
                                    AddToObjectProperties("north=" + r.North.Data, _numberObjs, _nullContext);
                                }
                            }
                        }
                        else if (BeginsWith(_lines[j], "south "))
                        {
                            r.South = GetTextOrScript(GetEverythingAfter(_lines[j], "south "));
                            if (_gameAslVersion >= 350)
                            {
                                if (r.South.Type == TextActionType.Script)
                                {
                                    AddObjectAction(_numberObjs, "south", r.South.Data);
                                }
                                else
                                {
                                    AddToObjectProperties("south=" + r.South.Data, _numberObjs, _nullContext);
                                }
                            }
                        }
                        else if (BeginsWith(_lines[j], "northeast "))
                        {
                            r.NorthEast = GetTextOrScript(GetEverythingAfter(_lines[j], "northeast "));
                            if (_gameAslVersion >= 350)
                            {
                                if (r.NorthEast.Type == TextActionType.Script)
                                {
                                    AddObjectAction(_numberObjs, "northeast", r.NorthEast.Data);
                                }
                                else
                                {
                                    AddToObjectProperties("northeast=" + r.NorthEast.Data, _numberObjs, _nullContext);
                                }
                            }
                        }
                        else if (BeginsWith(_lines[j], "northwest "))
                        {
                            r.NorthWest = GetTextOrScript(GetEverythingAfter(_lines[j], "northwest "));
                            if (_gameAslVersion >= 350)
                            {
                                if (r.NorthWest.Type == TextActionType.Script)
                                {
                                    AddObjectAction(_numberObjs, "northwest", r.NorthWest.Data);
                                }
                                else
                                {
                                    AddToObjectProperties("northwest=" + r.NorthWest.Data, _numberObjs, _nullContext);
                                }
                            }
                        }
                        else if (BeginsWith(_lines[j], "southeast "))
                        {
                            r.SouthEast = GetTextOrScript(GetEverythingAfter(_lines[j], "southeast "));
                            if (_gameAslVersion >= 350)
                            {
                                if (r.SouthEast.Type == TextActionType.Script)
                                {
                                    AddObjectAction(_numberObjs, "southeast", r.SouthEast.Data);
                                }
                                else
                                {
                                    AddToObjectProperties("southeast=" + r.SouthEast.Data, _numberObjs, _nullContext);
                                }
                            }
                        }
                        else if (BeginsWith(_lines[j], "southwest "))
                        {
                            r.SouthWest = GetTextOrScript(GetEverythingAfter(_lines[j], "southwest "));
                            if (_gameAslVersion >= 350)
                            {
                                if (r.SouthWest.Type == TextActionType.Script)
                                {
                                    AddObjectAction(_numberObjs, "southwest", r.SouthWest.Data);
                                }
                                else
                                {
                                    AddToObjectProperties("southwest=" + r.SouthWest.Data, _numberObjs, _nullContext);
                                }
                            }
                        }
                        else if (BeginsWith(_lines[j], "up "))
                        {
                            r.Up = GetTextOrScript(GetEverythingAfter(_lines[j], "up "));
                            if (_gameAslVersion >= 350)
                            {
                                if (r.Up.Type == TextActionType.Script)
                                {
                                    AddObjectAction(_numberObjs, "up", r.Up.Data);
                                }
                                else
                                {
                                    AddToObjectProperties("up=" + r.Up.Data, _numberObjs, _nullContext);
                                }
                            }
                        }
                        else if (BeginsWith(_lines[j], "down "))
                        {
                            r.Down = GetTextOrScript(GetEverythingAfter(_lines[j], "down "));
                            if (_gameAslVersion >= 350)
                            {
                                if (r.Down.Type == TextActionType.Script)
                                {
                                    AddObjectAction(_numberObjs, "down", r.Down.Data);
                                }
                                else
                                {
                                    AddToObjectProperties("down=" + r.Down.Data, _numberObjs, _nullContext);
                                }
                            }
                        }
                        else if (_gameAslVersion >= 280 & BeginsWith(_lines[j], "indescription "))
                        {
                            r.InDescription = GetParameter(_lines[j], _nullContext);
                            if (_gameAslVersion >= 350)
                                AddToObjectProperties("indescription=" + r.InDescription, _numberObjs, _nullContext);
                        }
                        else if (_gameAslVersion >= 280 & BeginsWith(_lines[j], "look "))
                        {
                            r.Look = GetParameter(_lines[j], _nullContext);
                            if (_gameAslVersion >= 350)
                                AddToObjectProperties("look=" + r.Look, _numberObjs, _nullContext);
                        }
                        else if (BeginsWith(_lines[j], "prefix "))
                        {
                            r.Prefix = GetParameter(_lines[j], _nullContext);
                            if (_gameAslVersion >= 350)
                                AddToObjectProperties("prefix=" + r.Prefix, _numberObjs, _nullContext);
                        }
                        else if (BeginsWith(_lines[j], "script "))
                        {
                            r.Script = GetEverythingAfter(_lines[j], "script ");
                            AddObjectAction(_numberObjs, "script", r.Script);
                        }
                        else if (BeginsWith(_lines[j], "command "))
                        {
                            r.NumberCommands = r.NumberCommands + 1;
                            Array.Resize(ref r.Commands, r.NumberCommands + 1);
                            r.Commands[r.NumberCommands] = new UserDefinedCommandType();
                            r.Commands[r.NumberCommands].CommandText = GetParameter(_lines[j], _nullContext, false);
                            r.Commands[r.NumberCommands].CommandScript = Strings.Trim(Strings.Mid(_lines[j], Strings.InStr(_lines[j], ">") + 1));
                        }
                        else if (BeginsWith(_lines[j], "place "))
                        {
                            r.NumberPlaces = r.NumberPlaces + 1;
                            Array.Resize(ref r.Places, r.NumberPlaces + 1);
                            r.Places[r.NumberPlaces] = new PlaceType();
                            string placeData = GetParameter(_lines[j], _nullContext);
                            int scp = Strings.InStr(placeData, ";");
                            if (scp == 0)
                            {
                                r.Places[r.NumberPlaces].PlaceName = placeData;
                            }
                            else
                            {
                                r.Places[r.NumberPlaces].PlaceName = Strings.Trim(Strings.Mid(placeData, scp + 1));
                                r.Places[r.NumberPlaces].Prefix = Strings.Trim(Strings.Left(placeData, scp - 1));
                            }
                            r.Places[r.NumberPlaces].Script = Strings.Trim(Strings.Mid(_lines[j], Strings.InStr(_lines[j], ">") + 1));
                        }
                        else if (BeginsWith(_lines[j], "use "))
                        {
                            r.NumberUse = r.NumberUse + 1;
                            Array.Resize(ref r.Use, r.NumberUse + 1);
                            r.Use[r.NumberUse] = new ScriptText();
                            r.Use[r.NumberUse].Text = GetParameter(_lines[j], _nullContext);
                            r.Use[r.NumberUse].Script = Strings.Trim(Strings.Mid(_lines[j], Strings.InStr(_lines[j], ">") + 1));
                        }
                        else if (BeginsWith(_lines[j], "properties "))
                        {
                            AddToObjectProperties(GetParameter(_lines[j], _nullContext), _numberObjs, _nullContext);
                        }
                        else if (BeginsWith(_lines[j], "type "))
                        {
                            _objs[_numberObjs].NumberTypesIncluded = _objs[_numberObjs].NumberTypesIncluded + 1;
                            Array.Resize(ref _objs[_numberObjs].TypesIncluded, _objs[_numberObjs].NumberTypesIncluded + 1);
                            _objs[_numberObjs].TypesIncluded[_objs[_numberObjs].NumberTypesIncluded] = GetParameter(_lines[j], _nullContext);

                            var propertyData = GetPropertiesInType(GetParameter(_lines[j], _nullContext));
                            AddToObjectProperties(propertyData.Properties, _numberObjs, _nullContext);
                            for (int k = 1, loopTo4 = propertyData.NumberActions; k <= loopTo4; k++)
                                AddObjectAction(_numberObjs, propertyData.Actions[k].ActionName, propertyData.Actions[k].Script);
                        }
                        else if (BeginsWith(_lines[j], "action "))
                        {
                            AddToObjectActions(GetEverythingAfter(_lines[j], "action "), _numberObjs, _nullContext);
                        }
                        else if (BeginsWith(_lines[j], "beforeturn "))
                        {
                            r.BeforeTurnScript = r.BeforeTurnScript + GetEverythingAfter(_lines[j], "beforeturn ") + Constants.vbCrLf;
                        }
                        else if (BeginsWith(_lines[j], "afterturn "))
                        {
                            r.AfterTurnScript = r.AfterTurnScript + GetEverythingAfter(_lines[j], "afterturn ") + Constants.vbCrLf;
                        }
                    }
                }
            }
        }

        private void SetUpSynonyms()
        {
            var block = GetDefineBlock("synonyms");
            _numberSynonyms = 0;

            if (block.StartLine == 0 & block.EndLine == 0)
            {
                return;
            }

            for (int i = block.StartLine + 1, loopTo = block.EndLine - 1; i <= loopTo; i++)
            {
                int eqp = Strings.InStr(_lines[i], "=");
                if (eqp != 0)
                {
                    string originalWordsList = Strings.Trim(Strings.Left(_lines[i], eqp - 1));
                    string convertWord = Strings.Trim(Strings.Mid(_lines[i], eqp + 1));

                    // Go through each word in OriginalWordsList (sep.
                    // by ";"):

                    originalWordsList = originalWordsList + ";";
                    int pos = 1;

                    do
                    {
                        int endOfWord = Strings.InStr(pos, originalWordsList, ";");
                        string thisWord = Strings.Trim(Strings.Mid(originalWordsList, pos, endOfWord - pos));

                        if (Strings.InStr(" " + convertWord + " ", " " + thisWord + " ") > 0)
                        {
                            // Recursive synonym
                            LogASLError("Recursive synonym detected: '" + thisWord + "' converting to '" + convertWord + "'", LogType.WarningError);
                        }
                        else
                        {
                            _numberSynonyms = _numberSynonyms + 1;
                            Array.Resize(ref _synonyms, _numberSynonyms + 1);
                            _synonyms[_numberSynonyms] = new SynonymType();
                            _synonyms[_numberSynonyms].OriginalWord = thisWord;
                            _synonyms[_numberSynonyms].ConvertTo = convertWord;
                        }
                        pos = endOfWord + 1;
                    }
                    while (pos < Strings.Len(originalWordsList));
                }
            }
        }

        private void SetUpTimers()
        {
            for (int i = 1, loopTo = _numberSections; i <= loopTo; i++)
            {
                if (BeginsWith(_lines[_defineBlocks[i].StartLine], "define timer "))
                {
                    _numberTimers = _numberTimers + 1;
                    Array.Resize(ref _timers, _numberTimers + 1);
                    _timers[_numberTimers] = new TimerType();
                    _timers[_numberTimers].TimerName = GetParameter(_lines[_defineBlocks[i].StartLine], _nullContext);
                    _timers[_numberTimers].TimerActive = false;

                    for (int j = _defineBlocks[i].StartLine + 1, loopTo1 = _defineBlocks[i].EndLine - 1; j <= loopTo1; j++)
                    {
                        if (BeginsWith(_lines[j], "interval "))
                        {
                            _timers[_numberTimers].TimerInterval = Conversions.ToInteger(GetParameter(_lines[j], _nullContext));
                        }
                        else if (BeginsWith(_lines[j], "action "))
                        {
                            _timers[_numberTimers].TimerAction = GetEverythingAfter(_lines[j], "action ");
                        }
                        else if (Strings.Trim(Strings.LCase(_lines[j])) == "enabled")
                        {
                            _timers[_numberTimers].TimerActive = true;
                        }
                        else if (Strings.Trim(Strings.LCase(_lines[j])) == "disabled")
                        {
                            _timers[_numberTimers].TimerActive = false;
                        }
                    }
                }
            }
        }

        private void SetUpTurnScript()
        {
            var block = GetDefineBlock("game");

            _beforeTurnScript = "";
            _afterTurnScript = "";

            for (int i = block.StartLine + 1, loopTo = block.EndLine - 1; i <= loopTo; i++)
            {
                if (BeginsWith(_lines[i], "beforeturn "))
                {
                    _beforeTurnScript = _beforeTurnScript + GetEverythingAfter(Strings.Trim(_lines[i]), "beforeturn ") + Constants.vbCrLf;
                }
                else if (BeginsWith(_lines[i], "afterturn "))
                {
                    _afterTurnScript = _afterTurnScript + GetEverythingAfter(Strings.Trim(_lines[i]), "afterturn ") + Constants.vbCrLf;
                }
            }
        }

        private void SetUpUserDefinedPlayerErrors()
        {
            // goes through "define game" block and sets stored error
            // messages accordingly

            var block = GetDefineBlock("game");
            bool examineIsCustomised = false;

            for (int i = block.StartLine + 1, loopTo = block.EndLine - 1; i <= loopTo; i++)
            {
                if (BeginsWith(_lines[i], "error "))
                {
                    string errorInfo = GetParameter(_lines[i], _nullContext, false);
                    int scp = Strings.InStr(errorInfo, ";");
                    string errorName = Strings.Left(errorInfo, scp - 1);
                    string errorMsg = Strings.Trim(Strings.Mid(errorInfo, scp + 1));
                    int currentError = 0;

                    switch (errorName ?? "")
                    {
                        case "badcommand":
                            {
                                currentError = (int)PlayerError.BadCommand;
                                break;
                            }
                        case "badgo":
                            {
                                currentError = (int)PlayerError.BadGo;
                                break;
                            }
                        case "badgive":
                            {
                                currentError = (int)PlayerError.BadGive;
                                break;
                            }
                        case "badcharacter":
                            {
                                currentError = (int)PlayerError.BadCharacter;
                                break;
                            }
                        case "noitem":
                            {
                                currentError = (int)PlayerError.NoItem;
                                break;
                            }
                        case "itemunwanted":
                            {
                                currentError = (int)PlayerError.ItemUnwanted;
                                break;
                            }
                        case "badlook":
                            {
                                currentError = (int)PlayerError.BadLook;
                                break;
                            }
                        case "badthing":
                            {
                                currentError = (int)PlayerError.BadThing;
                                break;
                            }
                        case "defaultlook":
                            {
                                currentError = (int)PlayerError.DefaultLook;
                                break;
                            }
                        case "defaultspeak":
                            {
                                currentError = (int)PlayerError.DefaultSpeak;
                                break;
                            }
                        case "baditem":
                            {
                                currentError = (int)PlayerError.BadItem;
                                break;
                            }
                        case "baddrop":
                            {
                                currentError = (int)PlayerError.BadDrop;
                                break;
                            }
                        case "defaultake":
                            {
                                if (_gameAslVersion <= 280)
                                {
                                    currentError = (int)PlayerError.BadTake;
                                }
                                else
                                {
                                    currentError = (int)PlayerError.DefaultTake;
                                }

                                break;
                            }
                        case "baduse":
                            {
                                currentError = (int)PlayerError.BadUse;
                                break;
                            }
                        case "defaultuse":
                            {
                                currentError = (int)PlayerError.DefaultUse;
                                break;
                            }
                        case "defaultout":
                            {
                                currentError = (int)PlayerError.DefaultOut;
                                break;
                            }
                        case "badplace":
                            {
                                currentError = (int)PlayerError.BadPlace;
                                break;
                            }
                        case "badexamine":
                            {
                                if (_gameAslVersion >= 310)
                                {
                                    currentError = (int)PlayerError.BadExamine;
                                }

                                break;
                            }
                        case "defaultexamine":
                            {
                                currentError = (int)PlayerError.DefaultExamine;
                                examineIsCustomised = true;
                                break;
                            }
                        case "badtake":
                            {
                                currentError = (int)PlayerError.BadTake;
                                break;
                            }
                        case "cantdrop":
                            {
                                currentError = (int)PlayerError.CantDrop;
                                break;
                            }
                        case "defaultdrop":
                            {
                                currentError = (int)PlayerError.DefaultDrop;
                                break;
                            }
                        case "badpronoun":
                            {
                                currentError = (int)PlayerError.BadPronoun;
                                break;
                            }
                        case "alreadyopen":
                            {
                                currentError = (int)PlayerError.AlreadyOpen;
                                break;
                            }
                        case "alreadyclosed":
                            {
                                currentError = (int)PlayerError.AlreadyClosed;
                                break;
                            }
                        case "cantopen":
                            {
                                currentError = (int)PlayerError.CantOpen;
                                break;
                            }
                        case "cantclose":
                            {
                                currentError = (int)PlayerError.CantClose;
                                break;
                            }
                        case "defaultopen":
                            {
                                currentError = (int)PlayerError.DefaultOpen;
                                break;
                            }
                        case "defaultclose":
                            {
                                currentError = (int)PlayerError.DefaultClose;
                                break;
                            }
                        case "badput":
                            {
                                currentError = (int)PlayerError.BadPut;
                                break;
                            }
                        case "cantput":
                            {
                                currentError = (int)PlayerError.CantPut;
                                break;
                            }
                        case "defaultput":
                            {
                                currentError = (int)PlayerError.DefaultPut;
                                break;
                            }
                        case "cantremove":
                            {
                                currentError = (int)PlayerError.CantRemove;
                                break;
                            }
                        case "alreadyput":
                            {
                                currentError = (int)PlayerError.AlreadyPut;
                                break;
                            }
                        case "defaultremove":
                            {
                                currentError = (int)PlayerError.DefaultRemove;
                                break;
                            }
                        case "locked":
                            {
                                currentError = (int)PlayerError.Locked;
                                break;
                            }
                        case "defaultwait":
                            {
                                currentError = (int)PlayerError.DefaultWait;
                                break;
                            }
                        case "alreadytaken":
                            {
                                currentError = (int)PlayerError.AlreadyTaken;
                                break;
                            }
                    }

                    _playerErrorMessageString[currentError] = errorMsg;
                    if (currentError == (int)PlayerError.DefaultLook & !examineIsCustomised)
                    {
                        // If we're setting the default look message, and we've not already customised the
                        // default examine message, then set the default examine message to the same thing.
                        _playerErrorMessageString[(int)PlayerError.DefaultExamine] = errorMsg;
                    }
                }
            }
        }

        private void SetVisibility(string thing, Thing @type, bool visible, Context ctx)
        {
            // Sets visibilty of objects and characters        

            if (_gameAslVersion >= 280)
            {
                bool found = false;

                for (int i = 1, loopTo = _numberObjs; i <= loopTo; i++)
                {
                    if ((Strings.LCase(_objs[i].ObjectName) ?? "") == (Strings.LCase(thing) ?? ""))
                    {
                        _objs[i].Visible = visible;
                        if (visible)
                        {
                            AddToObjectProperties("not invisible", i, ctx);
                        }
                        else
                        {
                            AddToObjectProperties("invisible", i, ctx);
                        }

                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    LogASLError("Not found object '" + thing + "'", LogType.WarningError);
                }
            }
            else
            {
                // split ThingString into character name and room
                // (thingstring of form name@room)

                int atPos = Strings.InStr(thing, "@");
                string room, name;

                // If no room specified, current room presumed
                if (atPos == 0)
                {
                    room = _currentRoom;
                    name = thing;
                }
                else
                {
                    name = Strings.Trim(Strings.Left(thing, atPos - 1));
                    room = Strings.Trim(Strings.Right(thing, Strings.Len(thing) - atPos));
                }

                if (type == Thing.Character)
                {
                    for (int i = 1, loopTo1 = _numberChars; i <= loopTo1; i++)
                    {
                        if ((Strings.LCase(_chars[i].ContainerRoom) ?? "") == (Strings.LCase(room) ?? "") & (Strings.LCase(_chars[i].ObjectName) ?? "") == (Strings.LCase(name) ?? ""))
                        {
                            _chars[i].Visible = visible;
                            break;
                        }
                    }
                }
                else if (type == Thing.Object)
                {
                    for (int i = 1, loopTo2 = _numberObjs; i <= loopTo2; i++)
                    {
                        if ((Strings.LCase(_objs[i].ContainerRoom) ?? "") == (Strings.LCase(room) ?? "") & (Strings.LCase(_objs[i].ObjectName) ?? "") == (Strings.LCase(name) ?? ""))
                        {
                            _objs[i].Visible = visible;
                            break;
                        }
                    }
                }
            }

            UpdateObjectList(ctx);
        }

        private void ShowPictureInText(string filename)
        {
            if (!_useStaticFrameForPictures)
            {
                _player.ShowPicture(filename);
            }
            else
            {
                // Workaround for a particular game which expects pictures to be in a popup window -
                // use the static picture frame feature so that image is not cleared
                _player.SetPanelContents("<img src=\"" + _player.GetURL(filename) + "\" onload=\"setPanelHeight()\"/>");
            }
        }

        private void ShowRoomInfoV2(string room)
        {
            // ShowRoomInfo for Quest 2.x games

            string roomDisplayText = "";
            bool descTagExist;
            DefineBlock gameBlock;
            string charsViewable;
            int charsFound;
            string prefixAliasNoFormat, prefix, prefixAlias, inDesc;
            string aliasName = "";
            string charList;
            int foundLastComma, cp, ncp;
            string noFormatObjsViewable;
            string objsViewable = "";
            var objsFound = default(int);
            string objListString, noFormatObjListString;
            string possDir, nsew, doorways, places, place;
            string aliasOut = "";
            string placeNoFormat;
            string descLine = "";
            int lastComma, oldLastComma;
            int defineBlock;
            string lookString = "";

            gameBlock = GetDefineBlock("game");
            _currentRoom = room;

            // find the room
            DefineBlock roomBlock;
            roomBlock = DefineBlockParam("room", room);
            bool finishedFindingCommas;

            charsViewable = "";
            charsFound = 0;

            // see if room has an alias
            for (int i = roomBlock.StartLine + 1, loopTo = roomBlock.EndLine - 1; i <= loopTo; i++)
            {
                if (BeginsWith(_lines[i], "alias"))
                {
                    aliasName = GetParameter(_lines[i], _nullContext);
                    i = roomBlock.EndLine;
                }
            }
            if (string.IsNullOrEmpty(aliasName))
                aliasName = room;

            // see if room has a prefix
            prefix = FindStatement(roomBlock, "prefix");
            if (string.IsNullOrEmpty(prefix))
            {
                prefixAlias = "|cr" + aliasName + "|cb";
                prefixAliasNoFormat = aliasName; // No formatting version, for label
            }
            else
            {
                prefixAlias = prefix + " |cr" + aliasName + "|cb";
                prefixAliasNoFormat = prefix + " " + aliasName;
            }

            // print player's location
            // find indescription line:
            inDesc = "unfound";
            for (int i = roomBlock.StartLine + 1, loopTo1 = roomBlock.EndLine - 1; i <= loopTo1; i++)
            {
                if (BeginsWith(_lines[i], "indescription"))
                {
                    inDesc = Strings.Trim(GetParameter(_lines[i], _nullContext));
                    i = roomBlock.EndLine;
                }
            }

            if (inDesc != "unfound")
            {
                // Print player's location according to indescription:
                if (Strings.Right(inDesc, 1) == ":")
                {
                    // if line ends with a colon, add place name:
                    roomDisplayText = roomDisplayText + Strings.Left(inDesc, Strings.Len(inDesc) - 1) + " " + prefixAlias + "." + Constants.vbCrLf;
                }
                else
                {
                    // otherwise, just print the indescription line:
                    roomDisplayText = roomDisplayText + inDesc + Constants.vbCrLf;
                }
            }
            else
            {
                // if no indescription line, print the default.
                roomDisplayText = roomDisplayText + "You are in " + prefixAlias + "." + Constants.vbCrLf;
            }

            _player.LocationUpdated(prefixAliasNoFormat);

            SetStringContents("quest.formatroom", prefixAliasNoFormat, _nullContext);

            // FIND CHARACTERS ===

            for (int i = 1, loopTo2 = _numberChars; i <= loopTo2; i++)
            {
                if ((_chars[i].ContainerRoom ?? "") == (room ?? "") & _chars[i].Exists & _chars[i].Visible)
                {
                    charsViewable = charsViewable + _chars[i].Prefix + "|b" + _chars[i].ObjectName + "|xb" + _chars[i].Suffix + ", ";
                    charsFound = charsFound + 1;
                }
            }

            if (charsFound == 0)
            {
                charsViewable = "There is nobody here.";
                SetStringContents("quest.characters", "", _nullContext);
            }
            else
            {
                // chop off final comma and add full stop (.)
                charList = Strings.Left(charsViewable, Strings.Len(charsViewable) - 2);
                SetStringContents("quest.characters", charList, _nullContext);

                // if more than one character, add "and" before
                // last one:
                cp = Strings.InStr(charList, ",");
                if (cp != 0)
                {
                    foundLastComma = 0;
                    do
                    {
                        ncp = Strings.InStr(cp + 1, charList, ",");
                        if (ncp == 0)
                        {
                            foundLastComma = 1;
                        }
                        else
                        {
                            cp = ncp;
                        }
                    }
                    while (foundLastComma != 1);

                    charList = Strings.Trim(Strings.Left(charList, cp - 1)) + " and " + Strings.Trim(Strings.Mid(charList, cp + 1));
                }

                charsViewable = "You can see " + charList + " here.";
            }

            roomDisplayText = roomDisplayText + charsViewable + Constants.vbCrLf;

            // FIND OBJECTS

            noFormatObjsViewable = "";

            for (int i = 1, loopTo3 = _numberObjs; i <= loopTo3; i++)
            {
                if ((_objs[i].ContainerRoom ?? "") == (room ?? "") & _objs[i].Exists & _objs[i].Visible)
                {
                    objsViewable = objsViewable + _objs[i].Prefix + "|b" + _objs[i].ObjectName + "|xb" + _objs[i].Suffix + ", ";
                    noFormatObjsViewable = noFormatObjsViewable + _objs[i].Prefix + _objs[i].ObjectName + ", ";

                    objsFound = objsFound + 1;
                }
            }

            var finishedLoop = default(bool);
            if (objsFound != 0)
            {
                objListString = Strings.Left(objsViewable, Strings.Len(objsViewable) - 2);
                noFormatObjListString = Strings.Left(noFormatObjsViewable, Strings.Len(noFormatObjsViewable) - 2);

                cp = Strings.InStr(objListString, ",");
                if (cp != 0)
                {
                    do
                    {
                        ncp = Strings.InStr(cp + 1, objListString, ",");
                        if (ncp == 0)
                        {
                            finishedLoop = true;
                        }
                        else
                        {
                            cp = ncp;
                        }
                    }
                    while (!finishedLoop);

                    objListString = Strings.Trim(Strings.Left(objListString, cp - 1) + " and " + Strings.Trim(Strings.Mid(objListString, cp + 1)));
                }

                objsViewable = "There is " + objListString + " here.";
                SetStringContents("quest.objects", Strings.Left(noFormatObjsViewable, Strings.Len(noFormatObjsViewable) - 2), _nullContext);
                SetStringContents("quest.formatobjects", objListString, _nullContext);
                roomDisplayText = roomDisplayText + objsViewable + Constants.vbCrLf;
            }
            else
            {
                SetStringContents("quest.objects", "", _nullContext);
                SetStringContents("quest.formatobjects", "", _nullContext);
            }

            // FIND DOORWAYS
            doorways = "";
            nsew = "";
            places = "";
            possDir = "";

            for (int i = roomBlock.StartLine + 1, loopTo4 = roomBlock.EndLine - 1; i <= loopTo4; i++)
            {
                if (BeginsWith(_lines[i], "out"))
                {
                    doorways = GetParameter(_lines[i], _nullContext);
                }

                if (BeginsWith(_lines[i], "north "))
                {
                    nsew = nsew + "|bnorth|xb, ";
                    possDir = possDir + "n";
                }
                else if (BeginsWith(_lines[i], "south "))
                {
                    nsew = nsew + "|bsouth|xb, ";
                    possDir = possDir + "s";
                }
                else if (BeginsWith(_lines[i], "east "))
                {
                    nsew = nsew + "|beast|xb, ";
                    possDir = possDir + "e";
                }
                else if (BeginsWith(_lines[i], "west "))
                {
                    nsew = nsew + "|bwest|xb, ";
                    possDir = possDir + "w";
                }
                else if (BeginsWith(_lines[i], "northeast "))
                {
                    nsew = nsew + "|bnortheast|xb, ";
                    possDir = possDir + "a";
                }
                else if (BeginsWith(_lines[i], "northwest "))
                {
                    nsew = nsew + "|bnorthwest|xb, ";
                    possDir = possDir + "b";
                }
                else if (BeginsWith(_lines[i], "southeast "))
                {
                    nsew = nsew + "|bsoutheast|xb, ";
                    possDir = possDir + "c";
                }
                else if (BeginsWith(_lines[i], "southwest "))
                {
                    nsew = nsew + "|bsouthwest|xb, ";
                    possDir = possDir + "d";
                }

                if (BeginsWith(_lines[i], "place"))
                {
                    // remove any prefix semicolon from printed text
                    place = GetParameter(_lines[i], _nullContext);
                    placeNoFormat = place; // Used in object list - no formatting or prefix
                    if (Strings.InStr(place, ";") > 0)
                    {
                        placeNoFormat = Strings.Right(place, Strings.Len(place) - (Strings.InStr(place, ";") + 1));
                        place = Strings.Trim(Strings.Left(place, Strings.InStr(place, ";") - 1)) + " |b" + Strings.Right(place, Strings.Len(place) - (Strings.InStr(place, ";") + 1)) + "|xb";
                    }
                    else
                    {
                        place = "|b" + place + "|xb";
                    }
                    places = places + place + ", ";

                }

            }

            DefineBlock outside;
            if (!string.IsNullOrEmpty(doorways))
            {
                // see if outside has an alias
                outside = DefineBlockParam("room", doorways);
                for (int i = outside.StartLine + 1, loopTo5 = outside.EndLine - 1; i <= loopTo5; i++)
                {
                    if (BeginsWith(_lines[i], "alias"))
                    {
                        aliasOut = GetParameter(_lines[i], _nullContext);
                        i = outside.EndLine;
                    }
                }
                if (string.IsNullOrEmpty(aliasOut))
                    aliasOut = doorways;

                roomDisplayText = roomDisplayText + "You can go out to " + aliasOut + "." + Constants.vbCrLf;
                possDir = possDir + "o";
                SetStringContents("quest.doorways.out", aliasOut, _nullContext);
            }
            else
            {
                SetStringContents("quest.doorways.out", "", _nullContext);
            }

            bool finished;
            if (!string.IsNullOrEmpty(nsew))
            {
                // strip final comma
                nsew = Strings.Left(nsew, Strings.Len(nsew) - 2);
                cp = Strings.InStr(nsew, ",");
                if (cp != 0)
                {
                    finished = false;
                    do
                    {
                        ncp = Strings.InStr(cp + 1, nsew, ",");
                        if (ncp == 0)
                        {
                            finished = true;
                        }
                        else
                        {
                            cp = ncp;
                        }
                    }
                    while (!finished);

                    nsew = Strings.Trim(Strings.Left(nsew, cp - 1)) + " or " + Strings.Trim(Strings.Mid(nsew, cp + 1));
                }

                roomDisplayText = roomDisplayText + "You can go " + nsew + "." + Constants.vbCrLf;
                SetStringContents("quest.doorways.dirs", nsew, _nullContext);
            }
            else
            {
                SetStringContents("quest.doorways.dirs", "", _nullContext);
            }

            UpdateDirButtons(possDir, _nullContext);

            if (!string.IsNullOrEmpty(places))
            {
                // strip final comma
                places = Strings.Left(places, Strings.Len(places) - 2);

                // if there is still a comma here, there is more than
                // one place, so add the word "or" before the last one.
                if (Strings.InStr(places, ",") > 0)
                {
                    lastComma = 0;
                    finishedFindingCommas = false;
                    do
                    {
                        oldLastComma = lastComma;
                        lastComma = Strings.InStr(lastComma + 1, places, ",");
                        if (lastComma == 0)
                        {
                            finishedFindingCommas = true;
                            lastComma = oldLastComma;
                        }
                    }
                    while (!finishedFindingCommas);

                    places = Strings.Left(places, lastComma) + " or" + Strings.Right(places, Strings.Len(places) - lastComma);
                }

                roomDisplayText = roomDisplayText + "You can go to " + places + "." + Constants.vbCrLf;
                SetStringContents("quest.doorways.places", places, _nullContext);
            }
            else
            {
                SetStringContents("quest.doorways.places", "", _nullContext);
            }

            // Print RoomDisplayText if there is no "description" tag,
            // otherwise execute the description tag information:

            // First, look in the "define room" block:
            descTagExist = false;
            for (int i = roomBlock.StartLine + 1, loopTo6 = roomBlock.EndLine - 1; i <= loopTo6; i++)
            {
                if (BeginsWith(_lines[i], "description "))
                {
                    descLine = _lines[i];
                    descTagExist = true;
                    break;
                }
            }

            if (descTagExist == false)
            {
                // Look in the "define game" block:
                for (int i = gameBlock.StartLine + 1, loopTo7 = gameBlock.EndLine - 1; i <= loopTo7; i++)
                {
                    if (BeginsWith(_lines[i], "description "))
                    {
                        descLine = _lines[i];
                        descTagExist = true;
                        break;
                    }
                }
            }

            if (descTagExist == false)
            {
                // Remove final newline:
                roomDisplayText = Strings.Left(roomDisplayText, Strings.Len(roomDisplayText) - 2);
                Print(roomDisplayText, _nullContext);
            }
            else
            {
                // execute description tag:
                // If no script, just print the tag's parameter.
                // Otherwise, execute it as ASL script:

                descLine = GetEverythingAfter(Strings.Trim(descLine), "description ");
                if (Strings.Left(descLine, 1) == "<")
                {
                    Print(GetParameter(descLine, _nullContext), _nullContext);
                }
                else
                {
                    ExecuteScript(descLine, _nullContext);
                }
            }

            UpdateObjectList(_nullContext);

            defineBlock = 0;

            for (int i = roomBlock.StartLine + 1, loopTo8 = roomBlock.EndLine - 1; i <= loopTo8; i++)
            {
                // don't get the 'look' statements in nested define blocks
                if (BeginsWith(_lines[i], "define"))
                    defineBlock = defineBlock + 1;
                if (BeginsWith(_lines[i], "end define"))
                    defineBlock = defineBlock - 1;
                if (BeginsWith(_lines[i], "look") & defineBlock == 0)
                {
                    lookString = GetParameter(_lines[i], _nullContext);
                    i = roomBlock.EndLine;
                }
            }

            if (!string.IsNullOrEmpty(lookString))
                Print(lookString, _nullContext);
        }

        private void Speak(string text)
        {
            _player.Speak(text);
        }

        private void AddToObjectList(List<ListData> objList, List<ListData> exitList, string name, Thing @type)
        {
            name = CapFirst(name);

            if (type == Thing.Room)
            {
                objList.Add(new ListData(name, _listVerbs[ListType.ExitsList]));
                exitList.Add(new ListData(name, _listVerbs[ListType.ExitsList]));
            }
            else
            {
                objList.Add(new ListData(name, _listVerbs[ListType.ObjectsList]));
            }
        }

        private void ExecExec(string scriptLine, Context ctx)
        {
            if (ctx.CancelExec)
                return;

            string execLine = GetParameter(scriptLine, ctx);
            var newCtx = CopyContext(ctx);
            newCtx.StackCounter = newCtx.StackCounter + 1;

            if (newCtx.StackCounter > 500)
            {
                LogASLError("Out of stack space running '" + scriptLine + "' - infinite loop?", LogType.WarningError);
                ctx.CancelExec = true;
                return;
            }

            if (_gameAslVersion >= 310)
            {
                newCtx.AllowRealNamesInCommand = true;
            }

            if (Strings.InStr(execLine, ";") == 0)
            {
                try
                {
                    ExecCommand(execLine, newCtx, false);
                }
                catch
                {
                    LogASLError("Internal error " + Information.Err().Number + " running '" + scriptLine + "'", LogType.WarningError);
                    ctx.CancelExec = true;
                }
            }
            else
            {
                int scp = Strings.InStr(execLine, ";");
                string ex = Strings.Trim(Strings.Left(execLine, scp - 1));
                string r = Strings.Trim(Strings.Mid(execLine, scp + 1));
                if (r == "normal")
                {
                    ExecCommand(ex, newCtx, false, false);
                }
                else
                {
                    LogASLError("Unrecognised post-command parameter in " + Strings.Trim(scriptLine), LogType.WarningError);
                }
            }
        }

        private void ExecSetString(string info, Context ctx)
        {
            // Sets string contents from a script parameter.
            // Eg <string1;contents> sets string variable string1
            // to "contents"

            int scp = Strings.InStr(info, ";");
            string name = Strings.Trim(Strings.Left(info, scp - 1));
            string value = Strings.Mid(info, scp + 1);

            if (Information.IsNumeric(name))
            {
                LogASLError("Invalid string name '" + name + "' - string names cannot be numeric", LogType.WarningError);
                return;
            }

            if (_gameAslVersion >= 281)
            {
                value = Strings.Trim(value);
                if (Strings.Left(value, 1) == "[" & Strings.Right(value, 1) == "]")
                {
                    value = Strings.Mid(value, 2, Strings.Len(value) - 2);
                }
            }

            var idx = GetArrayIndex(name, ctx);
            SetStringContents(idx.Name, value, ctx, idx.Index);
        }

        private bool ExecUserCommand(string cmd, Context ctx, bool libCommands = false)
        {
            // Executes a user-defined command. If unavailable, returns
            // false.
            string curCmd, commandList;
            string script = "";
            string commandTag;
            string commandLine = "";
            bool foundCommand = false;

            // First, check for a command in the current room block
            int roomId = GetRoomID(_currentRoom, ctx);

            // RoomID is 0 if we have no rooms in the game. Unlikely, but we get an RTE otherwise.
            if (roomId != 0)
            {
                var r = _rooms[roomId];
                for (int i = 1, loopTo = r.NumberCommands; i <= loopTo; i++)
                {
                    commandList = r.Commands[i].CommandText;
                    int ep;
                    bool exitFor = false;
                    do
                    {
                        ep = Strings.InStr(commandList, ";");
                        if (ep == 0)
                        {
                            curCmd = commandList;
                        }
                        else
                        {
                            curCmd = Strings.Trim(Strings.Left(commandList, ep - 1));
                            commandList = Strings.Trim(Strings.Mid(commandList, ep + 1));
                        }

                        if (IsCompatible(Strings.LCase(cmd), Strings.LCase(curCmd)))
                        {
                            commandLine = curCmd;
                            script = r.Commands[i].CommandScript;
                            foundCommand = true;
                            ep = 0;
                            exitFor = true;
                            break;
                        }
                    }
                    while (ep != 0);
                    if (exitFor)
                    {
                        break;
                    }
                }
            }

            if (!libCommands)
            {
                commandTag = "command";
            }
            else
            {
                commandTag = "lib command";
            }

            if (!foundCommand)
            {
                // Check "define game" block
                var block = GetDefineBlock("game");
                for (int i = block.StartLine + 1, loopTo1 = block.EndLine - 1; i <= loopTo1; i++)
                {
                    if (BeginsWith(_lines[i], commandTag))
                    {

                        commandList = GetParameter(_lines[i], ctx, false);
                        int ep;
                        bool exitFor1 = false;
                        do
                        {
                            ep = Strings.InStr(commandList, ";");
                            if (ep == 0)
                            {
                                curCmd = commandList;
                            }
                            else
                            {
                                curCmd = Strings.Trim(Strings.Left(commandList, ep - 1));
                                commandList = Strings.Trim(Strings.Mid(commandList, ep + 1));
                            }

                            if (IsCompatible(Strings.LCase(cmd), Strings.LCase(curCmd)))
                            {
                                commandLine = curCmd;
                                int ScriptPos = Strings.InStr(_lines[i], ">") + 1;
                                script = Strings.Trim(Strings.Mid(_lines[i], ScriptPos));
                                foundCommand = true;
                                ep = 0;
                                exitFor1 = true;
                                break;
                            }
                        }
                        while (ep != 0);
                        if (exitFor1)
                        {
                            break;
                        }
                    }
                }
            }

            if (foundCommand)
            {
                if (GetCommandParameters(cmd, commandLine, ctx))
                {
                    ExecuteScript(script, ctx);
                }
            }

            return foundCommand;
        }

        private void ExecuteChoose(string section, Context ctx)
        {
            ExecuteScript(SetUpChoiceForm(section, ctx), ctx);
        }

        private bool GetCommandParameters(string test, string @required, Context ctx)
        {
            // Gets parameters from line. For example, if 'required'
            // is "read #1#" and 'test' is "read sign", #1# returns
            // "sign".

            // Returns FALSE if #@object# form used and object doesn't
            // exist.

            var chunksBegin = default(int[]);
            var chunksEnd = default(int[]);
            var varName = default(string[]);
            var var2Pos = default(int);

            // Add dots before and after both strings. This fudge
            // stops problems caused when variables are right at the
            // beginning or end of a line.
            // PostScript: well, it used to, I'm not sure if it's really
            // required now though....
            // As of Quest 4.0 we use the ¦ character rather than a dot.
            test = "¦" + Strings.Trim(test) + "¦";
            @required = "¦" + @required + "¦";

            // Go through RequiredLine in chunks going up to variables.
            int currentReqLinePos = 1;
            int currentTestLinePos = 1;
            bool finished = false;
            int numberChunks = 0;
            do
            {
                int nextVarPos = Strings.InStr(currentReqLinePos, @required, "#");
                string currentVariable = "";

                if (nextVarPos == 0)
                {
                    finished = true;
                    nextVarPos = Strings.Len(@required) + 1;
                }
                else
                {
                    var2Pos = Strings.InStr(nextVarPos + 1, @required, "#");
                    currentVariable = Strings.Mid(@required, nextVarPos + 1, var2Pos - 1 - nextVarPos);
                }

                string checkChunk = Strings.Mid(@required, currentReqLinePos, nextVarPos - 1 - (currentReqLinePos - 1));
                int chunkBegin = Strings.InStr(currentTestLinePos, Strings.LCase(test), Strings.LCase(checkChunk));
                int chunkEnd = chunkBegin + Strings.Len(checkChunk);

                numberChunks = numberChunks + 1;
                Array.Resize(ref chunksBegin, numberChunks + 1);
                Array.Resize(ref chunksEnd, numberChunks + 1);
                Array.Resize(ref varName, numberChunks + 1);
                chunksBegin[numberChunks] = chunkBegin;
                chunksEnd[numberChunks] = chunkEnd;
                varName[numberChunks] = currentVariable;

                // Get to end of variable name
                currentReqLinePos = var2Pos + 1;

                currentTestLinePos = chunkEnd;
            }
            while (!finished);

            bool success = true;

            // Return values to string variable
            for (int i = 1, loopTo = numberChunks - 1; i <= loopTo; i++)
            {
                int arrayIndex;
                // If VarName contains array name, change to index number
                if (Strings.InStr(varName[i], "[") > 0)
                {
                    var indexResult = GetArrayIndex(varName[i], ctx);
                    varName[i] = indexResult.Name;
                    arrayIndex = indexResult.Index;
                }
                else
                {
                    arrayIndex = 0;
                }

                string curChunk = Strings.Mid(test, chunksEnd[i], chunksBegin[i + 1] - chunksEnd[i]);

                if (BeginsWith(varName[i], "@"))
                {
                    varName[i] = GetEverythingAfter(varName[i], "@");
                    int id = Disambiguate(curChunk, _currentRoom + ";" + "inventory", ctx);

                    if (id == -1)
                    {
                        if (_gameAslVersion >= 391)
                        {
                            PlayerErrorMessage(PlayerError.BadThing, ctx);
                        }
                        else
                        {
                            PlayerErrorMessage(PlayerError.BadItem, ctx);
                        }
                        // The Mid$(...,2) and Left$(...,2) removes the initial/final "."
                        _badCmdBefore = Strings.Mid(Strings.Trim(Strings.Left(test, chunksEnd[i] - 1)), 2);
                        _badCmdAfter = Strings.Trim(Strings.Mid(test, chunksBegin[i + 1]));
                        _badCmdAfter = Strings.Left(_badCmdAfter, Strings.Len(_badCmdAfter) - 1);
                        success = false;
                    }
                    else if (id == -2)
                    {
                        _badCmdBefore = Strings.Trim(Strings.Left(test, chunksEnd[i] - 1));
                        _badCmdAfter = Strings.Trim(Strings.Mid(test, chunksBegin[i + 1]));
                        success = false;
                    }
                    else
                    {
                        SetStringContents(varName[i], _objs[id].ObjectName, ctx, arrayIndex);
                    }
                }
                else
                {
                    SetStringContents(varName[i], curChunk, ctx, arrayIndex);
                }
            }

            return success;
        }

        private string GetGender(string character, bool capitalise, Context ctx)
        {
            string result;

            if (_gameAslVersion >= 281)
            {
                result = _objs[GetObjectIdNoAlias(character)].Gender;
            }
            else
            {
                string resultLine = RetrLine("character", character, "gender", ctx);

                if (resultLine == "<unfound>")
                {
                    result = "it ";
                }
                else
                {
                    result = GetParameter(resultLine, ctx) + " ";
                }
            }

            if (capitalise)
                result = Strings.UCase(Strings.Left(result, 1)) + Strings.Right(result, Strings.Len(result) - 1);
            return result;
        }

        private string GetStringContents(string name, Context ctx)
        {
            bool returnAlias = false;
            int arrayIndex = 0;

            // Check for property shortcut
            int cp = Strings.InStr(name, ":");
            if (cp != 0)
            {
                string objName = Strings.Trim(Strings.Left(name, cp - 1));
                string propName = Strings.Trim(Strings.Mid(name, cp + 1));

                int obp = Strings.InStr(objName, "(");
                if (obp != 0)
                {
                    int cbp = Strings.InStr(obp, objName, ")");
                    if (cbp != 0)
                    {
                        objName = GetStringContents(Strings.Mid(objName, obp + 1, cbp - obp - 1), ctx);
                    }
                }

                return GetObjectProperty(propName, GetObjectIdNoAlias(objName));
            }

            if (Strings.Left(name, 1) == "@")
            {
                returnAlias = true;
                name = Strings.Mid(name, 2);
            }

            if (Strings.InStr(name, "[") != 0 & Strings.InStr(name, "]") != 0)
            {
                int bp = Strings.InStr(name, "[");
                int ep = Strings.InStr(name, "]");
                string arrayIndexData = Strings.Mid(name, bp + 1, ep - bp - 1);
                if (Information.IsNumeric(arrayIndexData))
                {
                    arrayIndex = Conversions.ToInteger(arrayIndexData);
                }
                else
                {
                    arrayIndex = (int)Math.Round(GetNumericContents(arrayIndexData, ctx));
                    if (arrayIndex == -32767)
                    {
                        LogASLError("Array index in '" + name + "' is not valid. An array index must be either a number or a numeric variable (without surrounding '%' characters)", LogType.WarningError);
                        return "";
                    }
                }
                name = Strings.Left(name, bp - 1);
            }

            // First, see if the string already exists. If it does,
            // get its contents. If not, generate an error.

            bool exists = false;
            var id = default(int);

            if (_numberStringVariables > 0)
            {
                for (int i = 1, loopTo = _numberStringVariables; i <= loopTo; i++)
                {
                    if ((Strings.LCase(_stringVariable[i].VariableName) ?? "") == (Strings.LCase(name) ?? ""))
                    {
                        id = i;
                        exists = true;
                        break;
                    }
                }
            }

            if (!exists)
            {
                LogASLError("No string variable '" + name + "' defined.", LogType.WarningError);
                return "";
            }

            if (arrayIndex > _stringVariable[id].VariableUBound)
            {
                LogASLError("Array index of '" + name + "[" + Strings.Trim(Conversion.Str(arrayIndex)) + "]' too big.", LogType.WarningError);
                return "";
            }

            // Now, set the contents
            if (!returnAlias)
            {
                return _stringVariable[id].VariableContents[arrayIndex];
            }
            else
            {
                return _objs[GetObjectIdNoAlias(_stringVariable[id].VariableContents[arrayIndex])].ObjectAlias;
            }
        }

        private bool IsAvailable(string thingName, Thing @type, Context ctx)
        {
            // Returns availability of object/character

            // split ThingString into character name and room
            // (thingstring of form name@room)

            string room, name;

            int atPos = Strings.InStr(thingName, "@");

            // If no room specified, current room presumed
            if (atPos == 0)
            {
                room = _currentRoom;
                name = thingName;
            }
            else
            {
                name = Strings.Trim(Strings.Left(thingName, atPos - 1));
                room = Strings.Trim(Strings.Right(thingName, Strings.Len(thingName) - atPos));
            }

            if (type == Thing.Character)
            {
                for (int i = 1, loopTo = _numberChars; i <= loopTo; i++)
                {
                    if ((Strings.LCase(_chars[i].ContainerRoom) ?? "") == (Strings.LCase(room) ?? "") & (Strings.LCase(_chars[i].ObjectName) ?? "") == (Strings.LCase(name) ?? ""))
                    {
                        return _chars[i].Exists;
                    }
                }
            }
            else if (type == Thing.Object)
            {
                for (int i = 1, loopTo1 = _numberObjs; i <= loopTo1; i++)
                {
                    if ((Strings.LCase(_objs[i].ContainerRoom) ?? "") == (Strings.LCase(room) ?? "") & (Strings.LCase(_objs[i].ObjectName) ?? "") == (Strings.LCase(name) ?? ""))
                    {
                        return _objs[i].Exists;
                    }
                }
            }

            return default;
        }

        private bool IsCompatible(string test, string @required)
        {
            // Tests to see if 'test' "works" with 'required'.
            // For example, if 'required' = "read #text#", then the
            // tests of "read book" and "read sign" are compatible.
            var var2Pos = default(int);

            // This avoids "xxx123" being compatible with "xxx".
            test = "^" + Strings.Trim(test) + "^";
            @required = "^" + @required + "^";

            // Go through RequiredLine in chunks going up to variables.
            int currentReqLinePos = 1;
            int currentTestLinePos = 1;
            bool finished = false;
            do
            {
                int nextVarPos = Strings.InStr(currentReqLinePos, @required, "#");
                if (nextVarPos == 0)
                {
                    nextVarPos = Strings.Len(@required) + 1;
                    finished = true;
                }
                else
                {
                    var2Pos = Strings.InStr(nextVarPos + 1, @required, "#");
                }

                string checkChunk = Strings.Mid(@required, currentReqLinePos, nextVarPos - 1 - (currentReqLinePos - 1));

                if (Strings.InStr(currentTestLinePos, test, checkChunk) != 0)
                {
                    currentTestLinePos = Strings.InStr(currentTestLinePos, test, checkChunk) + Strings.Len(checkChunk);
                }
                else
                {
                    return false;
                }

                // Skip to end of variable
                currentReqLinePos = var2Pos + 1;
            }
            while (!finished);

            return true;
        }

        private bool OpenGame(string filename)
        {
            bool cdatb, result;
            bool visible;
            string room;
            string fileData = "";
            string savedQsgVersion;
            string data = "";
            string name;
            int scp, cdat;
            int scp2, scp3;
            string[] lines = null;

            _gameLoadMethod = "loaded";

            bool prevQsgVersion = false;

            // TODO: Need a way to pass in the QSG file data instead of reading it from the file system
            fileData = System.IO.File.ReadAllText(filename, System.Text.Encoding.GetEncoding(1252));

            // Check version
            savedQsgVersion = Strings.Left(fileData, 10);

            if (BeginsWith(savedQsgVersion, "QUEST200.1"))
            {
                prevQsgVersion = true;
            }
            else if (!BeginsWith(savedQsgVersion, "QUEST300"))
            {
                return false;
            }

            if (prevQsgVersion)
            {
                lines = fileData.Split(new[] { Constants.vbCrLf, Constants.vbLf }, StringSplitOptions.None);
                _gameFileName = lines[1];
            }
            else
            {
                InitFileData(fileData);
                GetNextChunk();

                _gameFileName = GetNextChunk();
            }

            if (!System.IO.File.Exists(_gameFileName))
            {
                _gameFileName = _player.GetNewGameFile(_gameFileName, "*.asl;*.cas;*.zip");
                if (string.IsNullOrEmpty(_gameFileName))
                    return false;
            }

            // TODO: Need to load the original game file here
            throw new NotImplementedException();
            // result = InitialiseGame(_gameFileName, True)

            if (result == false)
            {
                return false;
            }

            if (!prevQsgVersion)
            {
                // Open Quest 3.0 saved game file
                _gameLoading = true;
                RestoreGameData(fileData);
                _gameLoading = false;
            }
            else
            {
                // Open Quest 2.x saved game file

                _currentRoom = lines[3];

                // Start at line 5 as line 4 is always "!c"
                int lineNumber = 5;

                do
                {
                    data = lines[lineNumber];
                    lineNumber += 1;
                    if (data != "!i")
                    {
                        scp = Strings.InStr(data, ";");
                        name = Strings.Trim(Strings.Left(data, scp - 1));
                        cdat = Conversions.ToInteger(Strings.Right(data, Strings.Len(data) - scp));

                        for (int i = 1, loopTo = _numCollectables; i <= loopTo; i++)
                        {
                            if ((_collectables[i].Name ?? "") == (name ?? ""))
                            {
                                _collectables[i].Value = cdat;
                                i = _numCollectables;
                            }
                        }
                    }
                }
                while (data != "!i");

                do
                {
                    data = lines[lineNumber];
                    lineNumber += 1;
                    if (data != "!o")
                    {
                        scp = Strings.InStr(data, ";");
                        name = Strings.Trim(Strings.Left(data, scp - 1));
                        cdatb = IsYes(Strings.Right(data, Strings.Len(data) - scp));

                        for (int i = 1, loopTo1 = _numberItems; i <= loopTo1; i++)
                        {
                            if ((_items[i].Name ?? "") == (name ?? ""))
                            {
                                _items[i].Got = cdatb;
                                i = _numberItems;
                            }
                        }
                    }
                }
                while (data != "!o");

                do
                {
                    data = lines[lineNumber];
                    lineNumber += 1;
                    if (data != "!p")
                    {
                        scp = Strings.InStr(data, ";");
                        scp2 = Strings.InStr(scp + 1, data, ";");
                        scp3 = Strings.InStr(scp2 + 1, data, ";");

                        name = Strings.Trim(Strings.Left(data, scp - 1));
                        cdatb = IsYes(Strings.Mid(data, scp + 1, scp2 - scp - 1));
                        visible = IsYes(Strings.Mid(data, scp2 + 1, scp3 - scp2 - 1));
                        room = Strings.Trim(Strings.Mid(data, scp3 + 1));

                        for (int i = 1, loopTo2 = _numberObjs; i <= loopTo2; i++)
                        {
                            if ((_objs[i].ObjectName ?? "") == (name ?? "") & !_objs[i].Loaded)
                            {
                                _objs[i].Exists = cdatb;
                                _objs[i].Visible = visible;
                                _objs[i].ContainerRoom = room;
                                _objs[i].Loaded = true;
                                i = _numberObjs;
                            }
                        }
                    }
                }
                while (data != "!p");

                do
                {
                    data = lines[lineNumber];
                    lineNumber += 1;
                    if (data != "!s")
                    {
                        scp = Strings.InStr(data, ";");
                        scp2 = Strings.InStr(scp + 1, data, ";");
                        scp3 = Strings.InStr(scp2 + 1, data, ";");

                        name = Strings.Trim(Strings.Left(data, scp - 1));
                        cdatb = IsYes(Strings.Mid(data, scp + 1, scp2 - scp - 1));
                        visible = IsYes(Strings.Mid(data, scp2 + 1, scp3 - scp2 - 1));
                        room = Strings.Trim(Strings.Mid(data, scp3 + 1));

                        for (int i = 1, loopTo3 = _numberChars; i <= loopTo3; i++)
                        {
                            if ((_chars[i].ObjectName ?? "") == (name ?? ""))
                            {
                                _chars[i].Exists = cdatb;
                                _chars[i].Visible = visible;
                                _chars[i].ContainerRoom = room;
                                i = _numberChars;
                            }
                        }
                    }
                }
                while (data != "!s");

                do
                {
                    data = lines[lineNumber];
                    lineNumber += 1;
                    if (data != "!n")
                    {
                        scp = Strings.InStr(data, ";");
                        name = Strings.Trim(Strings.Left(data, scp - 1));
                        data = Strings.Right(data, Strings.Len(data) - scp);

                        SetStringContents(name, data, _nullContext);
                    }
                }
                while (data != "!n");

                do
                {
                    data = lines[lineNumber];
                    lineNumber += 1;
                    if (data != "!e")
                    {
                        scp = Strings.InStr(data, ";");
                        name = Strings.Trim(Strings.Left(data, scp - 1));
                        data = Strings.Right(data, Strings.Len(data) - scp);

                        SetNumericVariableContents(name, Conversion.Val(data), _nullContext);
                    }
                }
                while (data != "!e");

            }

            _saveGameFile = filename;

            return true;
        }

        private byte[] SaveGame(string filename, bool saveFile = true)
        {
            var ctx = new Context();
            string saveData;

            if (_gameAslVersion >= 391)
                ExecuteScript(_beforeSaveScript, ctx);

            if (_gameAslVersion >= 280)
            {
                saveData = MakeRestoreData();
            }
            else
            {
                saveData = MakeRestoreDataV2();
            }

            if (saveFile)
            {
                System.IO.File.WriteAllText(filename, saveData, System.Text.Encoding.GetEncoding(1252));
            }

            _saveGameFile = filename;

            return System.Text.Encoding.GetEncoding(1252).GetBytes(saveData);
        }

        private string MakeRestoreDataV2()
        {
            var lines = new List<string>();
            int i;

            lines.Add("QUEST200.1");
            lines.Add(GetOriginalFilenameForQSG());
            lines.Add(_gameName);
            lines.Add(_currentRoom);

            lines.Add("!c");
            var loopTo = _numCollectables;
            for (i = 1; i <= loopTo; i++)
                lines.Add(_collectables[i].Name + ";" + Conversion.Str(_collectables[i].Value));

            lines.Add("!i");
            var loopTo1 = _numberItems;
            for (i = 1; i <= loopTo1; i++)
                lines.Add(_items[i].Name + ";" + YesNo(_items[i].Got));

            lines.Add("!o");
            var loopTo2 = _numberObjs;
            for (i = 1; i <= loopTo2; i++)
                lines.Add(_objs[i].ObjectName + ";" + YesNo(_objs[i].Exists) + ";" + YesNo(_objs[i].Visible) + ";" + _objs[i].ContainerRoom);

            lines.Add("!p");
            var loopTo3 = _numberChars;
            for (i = 1; i <= loopTo3; i++)
                lines.Add(_chars[i].ObjectName + ";" + YesNo(_chars[i].Exists) + ";" + YesNo(_chars[i].Visible) + ";" + _chars[i].ContainerRoom);

            lines.Add("!s");
            var loopTo4 = _numberStringVariables;
            for (i = 1; i <= loopTo4; i++)
                lines.Add(_stringVariable[i].VariableName + ";" + _stringVariable[i].VariableContents[0]);

            lines.Add("!n");
            var loopTo5 = _numberNumericVariables;
            for (i = 1; i <= loopTo5; i++)
                lines.Add(_numericVariable[i].VariableName + ";" + Conversion.Str(Conversions.ToDouble(_numericVariable[i].VariableContents[0])));

            lines.Add("!e");

            return string.Join(Constants.vbCrLf, lines);
        }

        private void SetAvailability(string thingString, bool exists, Context ctx, Thing @type = Thing.Object)
        {
            // Sets availability of objects (and characters in ASL<281)

            if (_gameAslVersion >= 281)
            {
                bool found = false;
                for (int i = 1, loopTo = _numberObjs; i <= loopTo; i++)
                {
                    if ((Strings.LCase(_objs[i].ObjectName) ?? "") == (Strings.LCase(thingString) ?? ""))
                    {
                        _objs[i].Exists = exists;
                        if (exists)
                        {
                            AddToObjectProperties("not hidden", i, ctx);
                        }
                        else
                        {
                            AddToObjectProperties("hidden", i, ctx);
                        }
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    LogASLError("Not found object '" + thingString + "'", LogType.WarningError);
                }
            }
            else
            {
                // split ThingString into character name and room
                // (thingstring of form name@room)

                string room, name;

                int atPos = Strings.InStr(thingString, "@");
                // If no room specified, currentroom presumed
                if (atPos == 0)
                {
                    room = _currentRoom;
                    name = thingString;
                }
                else
                {
                    name = Strings.Trim(Strings.Left(thingString, atPos - 1));
                    room = Strings.Trim(Strings.Right(thingString, Strings.Len(thingString) - atPos));
                }
                if (type == Thing.Character)
                {
                    for (int i = 1, loopTo1 = _numberChars; i <= loopTo1; i++)
                    {
                        if ((Strings.LCase(_chars[i].ContainerRoom) ?? "") == (Strings.LCase(room) ?? "") & (Strings.LCase(_chars[i].ObjectName) ?? "") == (Strings.LCase(name) ?? ""))
                        {
                            _chars[i].Exists = exists;
                            break;
                        }
                    }
                }
                else if (type == Thing.Object)
                {
                    for (int i = 1, loopTo2 = _numberObjs; i <= loopTo2; i++)
                    {
                        if ((Strings.LCase(_objs[i].ContainerRoom) ?? "") == (Strings.LCase(room) ?? "") & (Strings.LCase(_objs[i].ObjectName) ?? "") == (Strings.LCase(name) ?? ""))
                        {
                            _objs[i].Exists = exists;
                            break;
                        }
                    }
                }
            }

            UpdateItems(ctx);
            UpdateObjectList(ctx);
        }

        internal void SetStringContents(string name, string value, Context ctx, int arrayIndex = 0)
        {
            var id = default(int);
            bool exists = false;

            if (string.IsNullOrEmpty(name))
            {
                LogASLError("Internal error - tried to set empty string name to '" + value + "'", LogType.WarningError);
                return;
            }

            if (_gameAslVersion >= 281)
            {
                int bp = Strings.InStr(name, "[");
                if (bp != 0)
                {
                    arrayIndex = GetArrayIndex(name, ctx).Index;
                    name = Strings.Left(name, bp - 1);
                }
            }

            if (arrayIndex < 0)
            {
                LogASLError("'" + name + "[" + Strings.Trim(Conversion.Str(arrayIndex)) + "]' is invalid - did not assign to array", LogType.WarningError);
                return;
            }

            // First, see if the string already exists. If it does,
            // modify it. If not, create it.

            if (_numberStringVariables > 0)
            {
                for (int i = 1, loopTo = _numberStringVariables; i <= loopTo; i++)
                {
                    if ((Strings.LCase(_stringVariable[i].VariableName) ?? "") == (Strings.LCase(name) ?? ""))
                    {
                        id = i;
                        exists = true;
                        break;
                    }
                }
            }

            if (!exists)
            {
                _numberStringVariables = _numberStringVariables + 1;
                id = _numberStringVariables;
                Array.Resize(ref _stringVariable, id + 1);
                _stringVariable[id] = new VariableType();
                _stringVariable[id].VariableUBound = arrayIndex;
            }

            if (arrayIndex > _stringVariable[id].VariableUBound)
            {
                Array.Resize(ref _stringVariable[id].VariableContents, arrayIndex + 1);
                _stringVariable[id].VariableUBound = arrayIndex;
            }

            // Now, set the contents
            _stringVariable[id].VariableName = name;
            Array.Resize(ref _stringVariable[id].VariableContents, _stringVariable[id].VariableUBound + 1);
            _stringVariable[id].VariableContents[arrayIndex] = value;

            if (!string.IsNullOrEmpty(_stringVariable[id].OnChangeScript))
            {
                string script = _stringVariable[id].OnChangeScript;
                ExecuteScript(script, ctx);
            }

            if (!string.IsNullOrEmpty(_stringVariable[id].DisplayString))
            {
                UpdateStatusVars(ctx);
            }
        }

        private void SetUpCharObjectInfo()
        {
            var defaultProperties = new PropertiesActions();

            _numberChars = 0;

            // see if define type <default> exists:
            bool defaultExists = false;
            for (int i = 1, loopTo = _numberSections; i <= loopTo; i++)
            {
                if (Strings.Trim(_lines[_defineBlocks[i].StartLine]) == "define type <default>")
                {
                    defaultExists = true;
                    defaultProperties = GetPropertiesInType("default");
                    break;
                }
            }

            for (int i = 1, loopTo1 = _numberSections; i <= loopTo1; i++)
            {
                var block = _defineBlocks[i];
                if (!(BeginsWith(_lines[block.StartLine], "define room") | BeginsWith(_lines[block.StartLine], "define game") | BeginsWith(_lines[block.StartLine], "define object ")))
                {
                    continue;
                }

                string restOfLine;
                string origContainerRoomName, containerRoomName;

                if (BeginsWith(_lines[block.StartLine], "define room"))
                {
                    origContainerRoomName = GetParameter(_lines[block.StartLine], _nullContext);
                }
                else
                {
                    origContainerRoomName = "";
                }

                int startLine = block.StartLine;
                int endLine = block.EndLine;

                if (BeginsWith(_lines[block.StartLine], "define object "))
                {
                    startLine = startLine - 1;
                    endLine = endLine + 1;
                }

                for (int j = startLine + 1, loopTo2 = endLine - 1; j <= loopTo2; j++)
                {
                    if (BeginsWith(_lines[j], "define object"))
                    {
                        containerRoomName = origContainerRoomName;

                        _numberObjs = _numberObjs + 1;
                        Array.Resize(ref _objs, _numberObjs + 1);
                        _objs[_numberObjs] = new ObjectType();

                        var o = _objs[_numberObjs];

                        o.ObjectName = GetParameter(_lines[j], _nullContext);
                        o.ObjectAlias = o.ObjectName;
                        o.DefinitionSectionStart = j;
                        o.ContainerRoom = containerRoomName;
                        o.Visible = true;
                        o.Gender = "it";
                        o.Article = "it";

                        o.Take.Type = TextActionType.Nothing;

                        if (defaultExists)
                        {
                            AddToObjectProperties(defaultProperties.Properties, _numberObjs, _nullContext);
                            for (int k = 1, loopTo3 = defaultProperties.NumberActions; k <= loopTo3; k++)
                                AddObjectAction(_numberObjs, defaultProperties.Actions[k].ActionName, defaultProperties.Actions[k].Script);
                        }

                        if (_gameAslVersion >= 391)
                            AddToObjectProperties("list", _numberObjs, _nullContext);

                        bool hidden = false;
                        do
                        {
                            j = j + 1;
                            if (Strings.Trim(_lines[j]) == "hidden")
                            {
                                o.Exists = false;
                                hidden = true;
                                if (_gameAslVersion >= 311)
                                    AddToObjectProperties("hidden", _numberObjs, _nullContext);
                            }
                            else if (BeginsWith(_lines[j], "startin ") & containerRoomName == "__UNKNOWN")
                            {
                                containerRoomName = GetParameter(_lines[j], _nullContext);
                            }
                            else if (BeginsWith(_lines[j], "prefix "))
                            {
                                o.Prefix = GetParameter(_lines[j], _nullContext) + " ";
                                if (_gameAslVersion >= 311)
                                    AddToObjectProperties("prefix=" + o.Prefix, _numberObjs, _nullContext);
                            }
                            else if (BeginsWith(_lines[j], "suffix "))
                            {
                                o.Suffix = GetParameter(_lines[j], _nullContext);
                                if (_gameAslVersion >= 311)
                                    AddToObjectProperties("suffix=" + o.Suffix, _numberObjs, _nullContext);
                            }
                            else if (Strings.Trim(_lines[j]) == "invisible")
                            {
                                o.Visible = false;
                                if (_gameAslVersion >= 311)
                                    AddToObjectProperties("invisible", _numberObjs, _nullContext);
                            }
                            else if (BeginsWith(_lines[j], "alias "))
                            {
                                o.ObjectAlias = GetParameter(_lines[j], _nullContext);
                                if (_gameAslVersion >= 311)
                                    AddToObjectProperties("alias=" + o.ObjectAlias, _numberObjs, _nullContext);
                            }
                            else if (BeginsWith(_lines[j], "alt "))
                            {
                                AddToObjectAltNames(GetParameter(_lines[j], _nullContext), _numberObjs);
                            }
                            else if (BeginsWith(_lines[j], "detail "))
                            {
                                o.Detail = GetParameter(_lines[j], _nullContext);
                                if (_gameAslVersion >= 311)
                                    AddToObjectProperties("detail=" + o.Detail, _numberObjs, _nullContext);
                            }
                            else if (BeginsWith(_lines[j], "gender "))
                            {
                                o.Gender = GetParameter(_lines[j], _nullContext);
                                if (_gameAslVersion >= 311)
                                    AddToObjectProperties("gender=" + o.Gender, _numberObjs, _nullContext);
                            }
                            else if (BeginsWith(_lines[j], "article "))
                            {
                                o.Article = GetParameter(_lines[j], _nullContext);
                                if (_gameAslVersion >= 311)
                                    AddToObjectProperties("article=" + o.Article, _numberObjs, _nullContext);
                            }
                            else if (BeginsWith(_lines[j], "gain "))
                            {
                                o.GainScript = GetEverythingAfter(_lines[j], "gain ");
                                AddObjectAction(_numberObjs, "gain", o.GainScript);
                            }
                            else if (BeginsWith(_lines[j], "lose "))
                            {
                                o.LoseScript = GetEverythingAfter(_lines[j], "lose ");
                                AddObjectAction(_numberObjs, "lose", o.LoseScript);
                            }
                            else if (BeginsWith(_lines[j], "displaytype "))
                            {
                                o.DisplayType = GetParameter(_lines[j], _nullContext);
                                if (_gameAslVersion >= 311)
                                    AddToObjectProperties("displaytype=" + o.DisplayType, _numberObjs, _nullContext);
                            }
                            else if (BeginsWith(_lines[j], "look "))
                            {
                                if (_gameAslVersion >= 311)
                                {
                                    restOfLine = GetEverythingAfter(_lines[j], "look ");
                                    if (Strings.Left(restOfLine, 1) == "<")
                                    {
                                        AddToObjectProperties("look=" + GetParameter(_lines[j], _nullContext), _numberObjs, _nullContext);
                                    }
                                    else
                                    {
                                        AddObjectAction(_numberObjs, "look", restOfLine);
                                    }
                                }
                            }
                            else if (BeginsWith(_lines[j], "examine "))
                            {
                                if (_gameAslVersion >= 311)
                                {
                                    restOfLine = GetEverythingAfter(_lines[j], "examine ");
                                    if (Strings.Left(restOfLine, 1) == "<")
                                    {
                                        AddToObjectProperties("examine=" + GetParameter(_lines[j], _nullContext), _numberObjs, _nullContext);
                                    }
                                    else
                                    {
                                        AddObjectAction(_numberObjs, "examine", restOfLine);
                                    }
                                }
                            }
                            else if (_gameAslVersion >= 311 & BeginsWith(_lines[j], "speak "))
                            {
                                restOfLine = GetEverythingAfter(_lines[j], "speak ");
                                if (Strings.Left(restOfLine, 1) == "<")
                                {
                                    AddToObjectProperties("speak=" + GetParameter(_lines[j], _nullContext), _numberObjs, _nullContext);
                                }
                                else
                                {
                                    AddObjectAction(_numberObjs, "speak", restOfLine);
                                }
                            }
                            else if (BeginsWith(_lines[j], "properties "))
                            {
                                AddToObjectProperties(GetParameter(_lines[j], _nullContext), _numberObjs, _nullContext);
                            }
                            else if (BeginsWith(_lines[j], "type "))
                            {
                                o.NumberTypesIncluded = o.NumberTypesIncluded + 1;
                                Array.Resize(ref o.TypesIncluded, o.NumberTypesIncluded + 1);
                                o.TypesIncluded[o.NumberTypesIncluded] = GetParameter(_lines[j], _nullContext);

                                var PropertyData = GetPropertiesInType(GetParameter(_lines[j], _nullContext));
                                AddToObjectProperties(PropertyData.Properties, _numberObjs, _nullContext);
                                for (int k = 1, loopTo4 = PropertyData.NumberActions; k <= loopTo4; k++)
                                    AddObjectAction(_numberObjs, PropertyData.Actions[k].ActionName, PropertyData.Actions[k].Script);

                                Array.Resize(ref o.TypesIncluded, o.NumberTypesIncluded + PropertyData.NumberTypesIncluded + 1);
                                for (int k = 1, loopTo5 = PropertyData.NumberTypesIncluded; k <= loopTo5; k++)
                                    o.TypesIncluded[k + o.NumberTypesIncluded] = PropertyData.TypesIncluded[k];
                                o.NumberTypesIncluded = o.NumberTypesIncluded + PropertyData.NumberTypesIncluded;
                            }
                            else if (BeginsWith(_lines[j], "action "))
                            {
                                AddToObjectActions(GetEverythingAfter(_lines[j], "action "), _numberObjs, _nullContext);
                            }
                            else if (BeginsWith(_lines[j], "use "))
                            {
                                AddToUseInfo(_numberObjs, GetEverythingAfter(_lines[j], "use "));
                            }
                            else if (BeginsWith(_lines[j], "give "))
                            {
                                AddToGiveInfo(_numberObjs, GetEverythingAfter(_lines[j], "give "));
                            }
                            else if (Strings.Trim(_lines[j]) == "take")
                            {
                                o.Take.Type = TextActionType.Default;
                                AddToObjectProperties("take", _numberObjs, _nullContext);
                            }
                            else if (BeginsWith(_lines[j], "take "))
                            {
                                if (Strings.Left(GetEverythingAfter(_lines[j], "take "), 1) == "<")
                                {
                                    o.Take.Type = TextActionType.Text;
                                    o.Take.Data = GetParameter(_lines[j], _nullContext);

                                    AddToObjectProperties("take=" + GetParameter(_lines[j], _nullContext), _numberObjs, _nullContext);
                                }
                                else
                                {
                                    o.Take.Type = TextActionType.Script;
                                    restOfLine = GetEverythingAfter(_lines[j], "take ");
                                    o.Take.Data = restOfLine;

                                    AddObjectAction(_numberObjs, "take", restOfLine);
                                }
                            }
                            else if (Strings.Trim(_lines[j]) == "container")
                            {
                                if (_gameAslVersion >= 391)
                                    AddToObjectProperties("container", _numberObjs, _nullContext);
                            }
                            else if (Strings.Trim(_lines[j]) == "surface")
                            {
                                if (_gameAslVersion >= 391)
                                {
                                    AddToObjectProperties("container", _numberObjs, _nullContext);
                                    AddToObjectProperties("surface", _numberObjs, _nullContext);
                                }
                            }
                            else if (Strings.Trim(_lines[j]) == "opened")
                            {
                                if (_gameAslVersion >= 391)
                                    AddToObjectProperties("opened", _numberObjs, _nullContext);
                            }
                            else if (Strings.Trim(_lines[j]) == "transparent")
                            {
                                if (_gameAslVersion >= 391)
                                    AddToObjectProperties("transparent", _numberObjs, _nullContext);
                            }
                            else if (Strings.Trim(_lines[j]) == "open")
                            {
                                AddToObjectProperties("open", _numberObjs, _nullContext);
                            }
                            else if (BeginsWith(_lines[j], "open "))
                            {
                                if (Strings.Left(GetEverythingAfter(_lines[j], "open "), 1) == "<")
                                {
                                    AddToObjectProperties("open=" + GetParameter(_lines[j], _nullContext), _numberObjs, _nullContext);
                                }
                                else
                                {
                                    restOfLine = GetEverythingAfter(_lines[j], "open ");
                                    AddObjectAction(_numberObjs, "open", restOfLine);
                                }
                            }
                            else if (Strings.Trim(_lines[j]) == "close")
                            {
                                AddToObjectProperties("close", _numberObjs, _nullContext);
                            }
                            else if (BeginsWith(_lines[j], "close "))
                            {
                                if (Strings.Left(GetEverythingAfter(_lines[j], "close "), 1) == "<")
                                {
                                    AddToObjectProperties("close=" + GetParameter(_lines[j], _nullContext), _numberObjs, _nullContext);
                                }
                                else
                                {
                                    restOfLine = GetEverythingAfter(_lines[j], "close ");
                                    AddObjectAction(_numberObjs, "close", restOfLine);
                                }
                            }
                            else if (Strings.Trim(_lines[j]) == "add")
                            {
                                AddToObjectProperties("add", _numberObjs, _nullContext);
                            }
                            else if (BeginsWith(_lines[j], "add "))
                            {
                                if (Strings.Left(GetEverythingAfter(_lines[j], "add "), 1) == "<")
                                {
                                    AddToObjectProperties("add=" + GetParameter(_lines[j], _nullContext), _numberObjs, _nullContext);
                                }
                                else
                                {
                                    restOfLine = GetEverythingAfter(_lines[j], "add ");
                                    AddObjectAction(_numberObjs, "add", restOfLine);
                                }
                            }
                            else if (Strings.Trim(_lines[j]) == "remove")
                            {
                                AddToObjectProperties("remove", _numberObjs, _nullContext);
                            }
                            else if (BeginsWith(_lines[j], "remove "))
                            {
                                if (Strings.Left(GetEverythingAfter(_lines[j], "remove "), 1) == "<")
                                {
                                    AddToObjectProperties("remove=" + GetParameter(_lines[j], _nullContext), _numberObjs, _nullContext);
                                }
                                else
                                {
                                    restOfLine = GetEverythingAfter(_lines[j], "remove ");
                                    AddObjectAction(_numberObjs, "remove", restOfLine);
                                }
                            }
                            else if (BeginsWith(_lines[j], "parent "))
                            {
                                AddToObjectProperties("parent=" + GetParameter(_lines[j], _nullContext), _numberObjs, _nullContext);
                            }
                            else if (BeginsWith(_lines[j], "list"))
                            {
                                ProcessListInfo(_lines[j], _numberObjs);
                            }
                        }

                        while (Strings.Trim(_lines[j]) != "end define");

                        o.DefinitionSectionEnd = j;
                        if (!hidden)
                            o.Exists = true;
                    }
                    else if (_gameAslVersion <= 280 & BeginsWith(_lines[j], "define character"))
                    {
                        containerRoomName = origContainerRoomName;
                        _numberChars = _numberChars + 1;
                        Array.Resize(ref _chars, _numberChars + 1);
                        _chars[_numberChars] = new ObjectType();
                        _chars[_numberChars].ObjectName = GetParameter(_lines[j], _nullContext);
                        _chars[_numberChars].DefinitionSectionStart = j;
                        _chars[_numberChars].ContainerRoom = "";
                        _chars[_numberChars].Visible = true;
                        bool hidden = false;
                        do
                        {
                            j = j + 1;
                            if (Strings.Trim(_lines[j]) == "hidden")
                            {
                                _chars[_numberChars].Exists = false;
                                hidden = true;
                            }
                            else if (BeginsWith(_lines[j], "startin ") & containerRoomName == "__UNKNOWN")
                            {
                                containerRoomName = GetParameter(_lines[j], _nullContext);
                            }
                            else if (BeginsWith(_lines[j], "prefix "))
                            {
                                _chars[_numberChars].Prefix = GetParameter(_lines[j], _nullContext) + " ";
                            }
                            else if (BeginsWith(_lines[j], "suffix "))
                            {
                                _chars[_numberChars].Suffix = " " + GetParameter(_lines[j], _nullContext);
                            }
                            else if (Strings.Trim(_lines[j]) == "invisible")
                            {
                                _chars[_numberChars].Visible = false;
                            }
                            else if (BeginsWith(_lines[j], "alias "))
                            {
                                _chars[_numberChars].ObjectAlias = GetParameter(_lines[j], _nullContext);
                            }
                            else if (BeginsWith(_lines[j], "detail "))
                            {
                                _chars[_numberChars].Detail = GetParameter(_lines[j], _nullContext);
                            }

                            _chars[_numberChars].ContainerRoom = containerRoomName;
                        }

                        while (Strings.Trim(_lines[j]) != "end define");

                        _chars[_numberChars].DefinitionSectionEnd = j;
                        if (!hidden)
                            _chars[_numberChars].Exists = true;
                    }
                }
            }

            UpdateVisibilityInContainers(_nullContext);
        }

        private void ShowGameAbout(Context ctx)
        {
            string version = FindStatement(GetDefineBlock("game"), "game version");
            string author = FindStatement(GetDefineBlock("game"), "game author");
            string copyright = FindStatement(GetDefineBlock("game"), "game copyright");
            string info = FindStatement(GetDefineBlock("game"), "game info");

            Print("|bGame name:|cl  " + _gameName + "|cb|xb", ctx);
            if (!string.IsNullOrEmpty(version))
                Print("|bVersion:|xb    " + version, ctx);
            if (!string.IsNullOrEmpty(author))
                Print("|bAuthor:|xb     " + author, ctx);
            if (!string.IsNullOrEmpty(copyright))
                Print("|bCopyright:|xb  " + copyright, ctx);

            if (!string.IsNullOrEmpty(info))
            {
                Print("", ctx);
                Print(info, ctx);
            }
        }

        private void ShowPicture(string filename)
        {
            // In Quest 4.x this function would be used for showing a picture in a popup window, but
            // this is no longer supported - ALL images are displayed in-line with the game text. Any
            // image caption is displayed as text, and any image size specified is ignored.

            string caption = "";

            if (Strings.InStr(filename, ";") != 0)
            {
                caption = Strings.Trim(Strings.Mid(filename, Strings.InStr(filename, ";") + 1));
                filename = Strings.Trim(Strings.Left(filename, Strings.InStr(filename, ";") - 1));
            }

            if (Strings.InStr(filename, "@") != 0)
            {
                // size is ignored
                filename = Strings.Trim(Strings.Left(filename, Strings.InStr(filename, "@") - 1));
            }

            if (caption.Length > 0)
                Print(caption, _nullContext);

            ShowPictureInText(filename);
        }

        private void ShowRoomInfo(string room, Context ctx, bool noPrint = false)
        {
            if (_gameAslVersion < 280)
            {
                ShowRoomInfoV2(room);
                return;
            }

            string roomDisplayText = "";
            bool descTagExist;
            string doorwayString, roomAlias;
            bool finishedFindingCommas;
            string prefix, roomDisplayName;
            string roomDisplayNameNoFormat, inDescription;
            string visibleObjects = "";
            string visibleObjectsNoFormat;
            string placeList;
            int lastComma, oldLastComma;
            var descType = default(int);
            string descLine = "";
            bool showLookText;
            string lookDesc = "";
            string objLook;
            string objSuffix;

            var gameBlock = GetDefineBlock("game");

            _currentRoom = room;
            int id = GetRoomID(_currentRoom, ctx);

            if (id == 0)
                return;

            // FIRST LINE - YOU ARE IN... ***********************************************

            roomAlias = _rooms[id].RoomAlias;
            if (string.IsNullOrEmpty(roomAlias))
                roomAlias = _rooms[id].RoomName;

            prefix = _rooms[id].Prefix;

            if (string.IsNullOrEmpty(prefix))
            {
                roomDisplayName = "|cr" + roomAlias + "|cb";
                roomDisplayNameNoFormat = roomAlias; // No formatting version, for label
            }
            else
            {
                roomDisplayName = prefix + " |cr" + roomAlias + "|cb";
                roomDisplayNameNoFormat = prefix + " " + roomAlias;
            }

            inDescription = _rooms[id].InDescription;

            if (!string.IsNullOrEmpty(inDescription))
            {
                // Print player's location according to indescription:
                if (Strings.Right(inDescription, 1) == ":")
                {
                    // if line ends with a colon, add place name:
                    roomDisplayText = roomDisplayText + Strings.Left(inDescription, Strings.Len(inDescription) - 1) + " " + roomDisplayName + "." + Constants.vbCrLf;
                }
                else
                {
                    // otherwise, just print the indescription line:
                    roomDisplayText = roomDisplayText + inDescription + Constants.vbCrLf;
                }
            }
            else
            {
                // if no indescription line, print the default.
                roomDisplayText = roomDisplayText + "You are in " + roomDisplayName + "." + Constants.vbCrLf;
            }

            _player.LocationUpdated(Strings.UCase(Strings.Left(roomAlias, 1)) + Strings.Mid(roomAlias, 2));

            SetStringContents("quest.formatroom", roomDisplayNameNoFormat, ctx);

            // SHOW OBJECTS *************************************************************

            visibleObjectsNoFormat = "";

            var visibleObjectsList = new List<int>(); // of object IDs
            var count = default(int);

            for (int i = 1, loopTo = _numberObjs; i <= loopTo; i++)
            {
                if ((Strings.LCase(_objs[i].ContainerRoom) ?? "") == (Strings.LCase(room) ?? "") & _objs[i].Exists & _objs[i].Visible & !_objs[i].IsExit)
                {
                    visibleObjectsList.Add(i);
                }
            }

            foreach (int objId in visibleObjectsList)
            {
                objSuffix = _objs[objId].Suffix;
                if (!string.IsNullOrEmpty(objSuffix))
                    objSuffix = " " + objSuffix;

                if (string.IsNullOrEmpty(_objs[objId].ObjectAlias))
                {
                    visibleObjects = visibleObjects + _objs[objId].Prefix + "|b" + _objs[objId].ObjectName + "|xb" + objSuffix;
                    visibleObjectsNoFormat = visibleObjectsNoFormat + _objs[objId].Prefix + _objs[objId].ObjectName;
                }
                else
                {
                    visibleObjects = visibleObjects + _objs[objId].Prefix + "|b" + _objs[objId].ObjectAlias + "|xb" + objSuffix;
                    visibleObjectsNoFormat = visibleObjectsNoFormat + _objs[objId].Prefix + _objs[objId].ObjectAlias;
                }

                count = count + 1;
                if (count < visibleObjectsList.Count - 1)
                {
                    visibleObjects = visibleObjects + ", ";
                    visibleObjectsNoFormat = visibleObjectsNoFormat + ", ";
                }
                else if (count == visibleObjectsList.Count - 1)
                {
                    visibleObjects = visibleObjects + " and ";
                    visibleObjectsNoFormat = visibleObjectsNoFormat + ", ";
                }
            }

            if (visibleObjectsList.Count > 0)
            {
                SetStringContents("quest.formatobjects", visibleObjects, ctx);
                visibleObjects = "There is " + visibleObjects + " here.";
                SetStringContents("quest.objects", visibleObjectsNoFormat, ctx);
                roomDisplayText = roomDisplayText + visibleObjects + Constants.vbCrLf;
            }
            else
            {
                SetStringContents("quest.objects", "", ctx);
                SetStringContents("quest.formatobjects", "", ctx);
            }

            // SHOW EXITS ***************************************************************

            doorwayString = UpdateDoorways(id, ctx);

            if (_gameAslVersion < 410)
            {
                placeList = GetGoToExits(id, ctx);

                if (!string.IsNullOrEmpty(placeList))
                {
                    // strip final comma
                    placeList = Strings.Left(placeList, Strings.Len(placeList) - 2);

                    // if there is still a comma here, there is more than
                    // one place, so add the word "or" before the last one.
                    if (Strings.InStr(placeList, ",") > 0)
                    {
                        lastComma = 0;
                        finishedFindingCommas = false;
                        do
                        {
                            oldLastComma = lastComma;
                            lastComma = Strings.InStr(lastComma + 1, placeList, ",");
                            if (lastComma == 0)
                            {
                                finishedFindingCommas = true;
                                lastComma = oldLastComma;
                            }
                        }
                        while (!finishedFindingCommas);

                        placeList = Strings.Left(placeList, lastComma - 1) + " or" + Strings.Right(placeList, Strings.Len(placeList) - lastComma);
                    }

                    roomDisplayText = roomDisplayText + "You can go to " + placeList + "." + Constants.vbCrLf;
                    SetStringContents("quest.doorways.places", placeList, ctx);
                }
                else
                {
                    SetStringContents("quest.doorways.places", "", ctx);
                }
            }

            // GET "LOOK" DESCRIPTION (but don't print it yet) **************************

            objLook = GetObjectProperty("look", _rooms[id].ObjId, logError: false);

            if (string.IsNullOrEmpty(objLook))
            {
                if (!string.IsNullOrEmpty(_rooms[id].Look))
                {
                    lookDesc = _rooms[id].Look;
                }
            }
            else
            {
                lookDesc = objLook;
            }

            SetStringContents("quest.lookdesc", lookDesc, ctx);


            // FIND DESCRIPTION TAG, OR ACTION ******************************************

            // In Quest versions prior to 3.1, with any custom description, the "look"
            // text was always displayed after the "description" tag was printed/executed.
            // In Quest 3.1 and later, it isn't - descriptions should print the look
            // tag themselves when and where necessary.

            showLookText = true;

            if (!string.IsNullOrEmpty(_rooms[id].Description.Data))
            {
                descLine = _rooms[id].Description.Data;
                descType = (int)_rooms[id].Description.Type;
                descTagExist = true;
            }
            else
            {
                descTagExist = false;
            }

            if (descTagExist == false)
            {
                // Look in the "define game" block:
                for (int i = gameBlock.StartLine + 1, loopTo1 = gameBlock.EndLine - 1; i <= loopTo1; i++)
                {
                    if (BeginsWith(_lines[i], "description "))
                    {
                        descLine = GetEverythingAfter(_lines[i], "description ");
                        descTagExist = true;
                        if (Strings.Left(descLine, 1) == "<")
                        {
                            descLine = GetParameter(descLine, ctx);
                            descType = (int)TextActionType.Text;
                        }
                        else
                        {
                            descType = (int)TextActionType.Script;
                        }
                        i = gameBlock.EndLine;
                    }
                }
            }

            if (descTagExist & _gameAslVersion >= 310)
            {
                showLookText = false;
            }

            if (!noPrint)
            {
                if (descTagExist == false)
                {
                    // Remove final vbCrLf:
                    roomDisplayText = Strings.Left(roomDisplayText, Strings.Len(roomDisplayText) - 2);
                    Print(roomDisplayText, ctx);
                    if (!string.IsNullOrEmpty(doorwayString))
                        Print(doorwayString, ctx);
                }
                // execute description tag:
                // If no script, just print the tag's parameter.
                // Otherwise, execute it as ASL script:

                else if (descType == (int)TextActionType.Text)
                {
                    Print(descLine, ctx);
                }
                else
                {
                    ExecuteScript(descLine, ctx);
                }

                UpdateObjectList(ctx);

                // SHOW "LOOK" DESCRIPTION **************************************************

                if (showLookText)
                {
                    if (!string.IsNullOrEmpty(lookDesc))
                    {
                        Print(lookDesc, ctx);
                    }
                }
            }

        }

        private void CheckCollectable(int id)
        {
            // Checks to see whether a collectable item has exceeded
            // its range - if so, it resets the number to the nearest
            // valid number. It's a handy quick way of making sure that
            // a player's health doesn't reach 101%, for example.

            double max = default, value, min = default;
            int m;

            string @type = _collectables[id].Type;
            value = _collectables[id].Value;

            if (type == "%" & value > 100d)
                value = 100d;
            if ((type == "%" | type == "p") & value < 0d)
                value = 0d;
            if (Strings.InStr(type, "r") > 0)
            {
                if (Strings.InStr(type, "r") == 1)
                {
                    max = Conversion.Val(Strings.Mid(type, Strings.Len(type) - 1));
                    m = 1;
                }
                else if (Strings.InStr(type, "r") == Strings.Len(type))
                {
                    min = Conversion.Val(Strings.Left(type, Strings.Len(type) - 1));
                    m = 2;
                }
                else
                {
                    min = Conversion.Val(Strings.Left(type, Strings.InStr(type, "r") - 1));
                    max = Conversion.Val(Strings.Mid(type, Strings.InStr(type, "r") + 1));
                    m = 3;
                }

                if ((m == 1 | m == 3) & value > max)
                    value = max;
                if ((m == 2 | m == 3) & value < min)
                    value = min;
            }

            _collectables[id].Value = value;
        }

        private string DisplayCollectableInfo(int id)
        {
            string display;

            if (_collectables[id].Display == "<def>")
            {
                display = "You have " + Strings.Trim(Conversion.Str(_collectables[id].Value)) + " " + _collectables[id].Name;
            }
            else if (string.IsNullOrEmpty(_collectables[id].Display))
            {
                display = "<null>";
            }
            else
            {
                int ep = Strings.InStr(_collectables[id].Display, "!");
                if (ep == 0)
                {
                    display = _collectables[id].Display;
                }
                else
                {
                    string firstBit = Strings.Left(_collectables[id].Display, ep - 1);
                    string nextBit = Strings.Right(_collectables[id].Display, Strings.Len(_collectables[id].Display) - ep);
                    display = firstBit + Strings.Trim(Conversion.Str(_collectables[id].Value)) + nextBit;
                }

                if (Strings.InStr(display, "*") > 0)
                {
                    int firstStarPos = Strings.InStr(display, "*");
                    int secondStarPos = Strings.InStr(firstStarPos + 1, display, "*");
                    string beforeStar = Strings.Left(display, firstStarPos - 1);
                    string afterStar = Strings.Mid(display, secondStarPos + 1);
                    string betweenStar = Strings.Mid(display, firstStarPos + 1, secondStarPos - firstStarPos - 1);

                    if (_collectables[id].Value != 1d)
                    {
                        display = beforeStar + betweenStar + afterStar;
                    }
                    else
                    {
                        display = beforeStar + afterStar;
                    }
                }
            }

            if (_collectables[id].Value == 0d & _collectables[id].DisplayWhenZero == false)
            {
                display = "<null>";
            }

            return display;
        }

        private void DisplayTextSection(string section, Context ctx)
        {
            DefineBlock block;
            block = DefineBlockParam("text", section);

            if (block.StartLine == 0)
            {
                return;
            }

            for (int i = block.StartLine + 1, loopTo = block.EndLine - 1; i <= loopTo; i++)
            {
                if (_gameAslVersion >= 392)
                {
                    // Convert string variables etc.
                    Print(GetParameter("<" + _lines[i] + ">", ctx), ctx);
                }
                else
                {
                    Print(_lines[i], ctx);
                }
            }

            Print("", ctx);
        }

        // Returns true if the system is ready to process a new command after completion - so it will be
        // in most cases, except when ExecCommand just caused an "enter" script command to complete

        private bool ExecCommand(string input, Context ctx, bool echo = true, bool runUserCommand = true, bool dontSetIt = false)
        {
            string parameter;
            bool skipAfterTurn = false;
            input = RemoveFormatting(input);

            string oldBadCmdBefore = _badCmdBefore;

            int roomID = GetRoomID(_currentRoom, ctx);

            if (string.IsNullOrEmpty(input))
                return true;

            string cmd = Strings.LCase(input);

            lock (_commandLock)
            {
                if (_commandOverrideModeOn)
                {
                    // Commands have been overridden for this command,
                    // so put input into previously specified variable
                    // and exit:

                    SetStringContents(_commandOverrideVariable, input, ctx);
                    System.Threading.Monitor.PulseAll(_commandLock);
                    return false;
                }
            }

            bool userCommandReturn;

            if (echo)
            {
                Print("> " + input, ctx);
            }

            input = Strings.LCase(input);

            SetStringContents("quest.originalcommand", input, ctx);

            string newCommand = " " + input + " ";

            // Convert synonyms:
            for (int i = 1, loopTo = _numberSynonyms; i <= loopTo; i++)
            {
                int cp = 1;
                int n;
                do
                {
                    n = Strings.InStr(cp, newCommand, " " + _synonyms[i].OriginalWord + " ");
                    if (n != 0)
                    {
                        newCommand = Strings.Left(newCommand, n - 1) + " " + _synonyms[i].ConvertTo + " " + Strings.Mid(newCommand, n + Strings.Len(_synonyms[i].OriginalWord) + 2);
                        cp = n + 1;
                    }
                }
                while (n != 0);
            }

            // strip starting and ending spaces
            input = Strings.Mid(newCommand, 2, Strings.Len(newCommand) - 2);

            SetStringContents("quest.command", input, ctx);

            // Execute any "beforeturn" script:

            var newCtx = CopyContext(ctx);
            bool globalOverride = false;

            // RoomID is 0 if there are no rooms in the game. Unlikely, but we get an RTE otherwise.
            if (roomID != 0)
            {
                if (!string.IsNullOrEmpty(_rooms[roomID].BeforeTurnScript))
                {
                    if (BeginsWith(_rooms[roomID].BeforeTurnScript, "override"))
                    {
                        ExecuteScript(GetEverythingAfter(_rooms[roomID].BeforeTurnScript, "override"), newCtx);
                        globalOverride = true;
                    }
                    else
                    {
                        ExecuteScript(_rooms[roomID].BeforeTurnScript, newCtx);
                    }
                }
            }
            if (!string.IsNullOrEmpty(_beforeTurnScript) & globalOverride == false)
                ExecuteScript(_beforeTurnScript, newCtx);

            // In executing BeforeTurn script, "dontprocess" sets ctx.DontProcessCommand,
            // in which case we don't process the command.

            if (!newCtx.DontProcessCommand)
            {
                // Try to execute user defined command, if allowed:

                userCommandReturn = false;
                if (runUserCommand == true)
                {
                    userCommandReturn = ExecUserCommand(input, ctx);

                    if (!userCommandReturn)
                    {
                        userCommandReturn = ExecVerb(input, ctx);
                    }

                    if (!userCommandReturn)
                    {
                        // Try command defined by a library
                        userCommandReturn = ExecUserCommand(input, ctx, true);
                    }

                    if (!userCommandReturn)
                    {
                        // Try verb defined by a library
                        userCommandReturn = ExecVerb(input, ctx, true);
                    }
                }

                input = Strings.LCase(input);
            }
            else
            {
                // Set the UserCommand flag to fudge not processing any more commands
                userCommandReturn = true;
            }

            string invList = "";

            if (!userCommandReturn)
            {
                if (CmdStartsWith(input, "speak to "))
                {
                    parameter = GetEverythingAfter(input, "speak to ");
                    ExecSpeak(parameter, ctx);
                }
                else if (CmdStartsWith(input, "talk to "))
                {
                    parameter = GetEverythingAfter(input, "talk to ");
                    ExecSpeak(parameter, ctx);
                }
                else if (cmd == "exit" | cmd == "out" | cmd == "leave")
                {
                    GoDirection("out", ctx);
                    _lastIt = 0;
                }
                else if (cmd == "north" | cmd == "south" | cmd == "east" | cmd == "west")
                {
                    GoDirection(input, ctx);
                    _lastIt = 0;
                }
                else if (cmd == "n" | cmd == "s" | cmd == "w" | cmd == "e")
                {
                    switch (Strings.InStr("nswe", cmd))
                    {
                        case 1:
                            {
                                GoDirection("north", ctx);
                                break;
                            }
                        case 2:
                            {
                                GoDirection("south", ctx);
                                break;
                            }
                        case 3:
                            {
                                GoDirection("west", ctx);
                                break;
                            }
                        case 4:
                            {
                                GoDirection("east", ctx);
                                break;
                            }
                    }
                    _lastIt = 0;
                }
                else if (cmd == "ne" | cmd == "northeast" | cmd == "north-east" | cmd == "north east" | cmd == "go ne" | cmd == "go northeast" | cmd == "go north-east" | cmd == "go north east")
                {
                    GoDirection("northeast", ctx);
                    _lastIt = 0;
                }
                else if (cmd == "nw" | cmd == "northwest" | cmd == "north-west" | cmd == "north west" | cmd == "go nw" | cmd == "go northwest" | cmd == "go north-west" | cmd == "go north west")
                {
                    GoDirection("northwest", ctx);
                    _lastIt = 0;
                }
                else if (cmd == "se" | cmd == "southeast" | cmd == "south-east" | cmd == "south east" | cmd == "go se" | cmd == "go southeast" | cmd == "go south-east" | cmd == "go south east")
                {
                    GoDirection("southeast", ctx);
                    _lastIt = 0;
                }
                else if (cmd == "sw" | cmd == "southwest" | cmd == "south-west" | cmd == "south west" | cmd == "go sw" | cmd == "go southwest" | cmd == "go south-west" | cmd == "go south west")
                {
                    GoDirection("southwest", ctx);
                    _lastIt = 0;
                }
                else if (cmd == "up" | cmd == "u")
                {
                    GoDirection("up", ctx);
                    _lastIt = 0;
                }
                else if (cmd == "down" | cmd == "d")
                {
                    GoDirection("down", ctx);
                    _lastIt = 0;
                }
                else if (CmdStartsWith(input, "go "))
                {
                    if (_gameAslVersion >= 410)
                    {
                        _rooms[GetRoomID(_currentRoom, ctx)].Exits.ExecuteGo(input, ref ctx);
                    }
                    else
                    {
                        parameter = GetEverythingAfter(input, "go ");
                        if (parameter == "out")
                        {
                            GoDirection("out", ctx);
                        }
                        else if (parameter == "north" | parameter == "south" | parameter == "east" | parameter == "west" | parameter == "up" | parameter == "down")
                        {
                            GoDirection(parameter, ctx);
                        }
                        else if (BeginsWith(parameter, "to "))
                        {
                            parameter = GetEverythingAfter(parameter, "to ");
                            GoToPlace(parameter, ctx);
                        }
                        else
                        {
                            PlayerErrorMessage(PlayerError.BadGo, ctx);
                        }
                    }
                    _lastIt = 0;
                }
                else if (CmdStartsWith(input, "give "))
                {
                    parameter = GetEverythingAfter(input, "give ");
                    ExecGive(parameter, ctx);
                }
                else if (CmdStartsWith(input, "take "))
                {
                    parameter = GetEverythingAfter(input, "take ");
                    ExecTake(parameter, ctx);
                }
                else if (CmdStartsWith(input, "drop ") & _gameAslVersion >= 280)
                {
                    parameter = GetEverythingAfter(input, "drop ");
                    ExecDrop(parameter, ctx);
                }
                else if (CmdStartsWith(input, "get "))
                {
                    parameter = GetEverythingAfter(input, "get ");
                    ExecTake(parameter, ctx);
                }
                else if (CmdStartsWith(input, "pick up "))
                {
                    parameter = GetEverythingAfter(input, "pick up ");
                    ExecTake(parameter, ctx);
                }
                else if (cmd == "pick it up" | cmd == "pick them up" | cmd == "pick this up" | cmd == "pick that up" | cmd == "pick these up" | cmd == "pick those up" | cmd == "pick him up" | cmd == "pick her up")
                {
                    ExecTake(Strings.Mid(cmd, 6, Strings.InStr(7, cmd, " ") - 6), ctx);
                }
                else if (CmdStartsWith(input, "look "))
                {
                    ExecLook(input, ctx);
                }
                else if (CmdStartsWith(input, "l "))
                {
                    ExecLook("look " + GetEverythingAfter(input, "l "), ctx);
                }
                else if (CmdStartsWith(input, "examine ") & _gameAslVersion >= 280)
                {
                    ExecExamine(input, ctx);
                }
                else if (CmdStartsWith(input, "x ") & _gameAslVersion >= 280)
                {
                    ExecExamine("examine " + GetEverythingAfter(input, "x "), ctx);
                }
                else if (cmd == "l" | cmd == "look")
                {
                    ExecLook("look", ctx);
                }
                else if (cmd == "x" | cmd == "examine")
                {
                    ExecExamine("examine", ctx);
                }
                else if (CmdStartsWith(input, "use "))
                {
                    ExecUse(input, ctx);
                }
                else if (CmdStartsWith(input, "open ") & _gameAslVersion >= 391)
                {
                    ExecOpenClose(input, ctx);
                }
                else if (CmdStartsWith(input, "close ") & _gameAslVersion >= 391)
                {
                    ExecOpenClose(input, ctx);
                }
                else if (CmdStartsWith(input, "put ") & _gameAslVersion >= 391)
                {
                    ExecAddRemove(input, ctx);
                }
                else if (CmdStartsWith(input, "add ") & _gameAslVersion >= 391)
                {
                    ExecAddRemove(input, ctx);
                }
                else if (CmdStartsWith(input, "remove ") & _gameAslVersion >= 391)
                {
                    ExecAddRemove(input, ctx);
                }
                else if (cmd == "save")
                {
                    _player.RequestSave(null);
                }
                else if (cmd == "quit")
                {
                    GameFinished();
                }
                else if (BeginsWith(cmd, "help"))
                {
                    ShowHelp(ctx);
                }
                else if (cmd == "about")
                {
                    ShowGameAbout(ctx);
                }
                else if (cmd == "clear")
                {
                    DoClear();
                }
                else if (cmd == "debug")
                {
                    // TO DO: This is temporary, would be better to have a log viewer built in to Player
                    foreach (string logEntry in _log)
                        Print(logEntry, ctx);
                }
                else if (cmd == "inventory" | cmd == "inv" | cmd == "i")
                {
                    if (_gameAslVersion >= 280)
                    {
                        for (int i = 1, loopTo1 = _numberObjs; i <= loopTo1; i++)
                        {
                            if (_objs[i].ContainerRoom == "inventory" & _objs[i].Exists & _objs[i].Visible)
                            {
                                invList = invList + _objs[i].Prefix;

                                if (string.IsNullOrEmpty(_objs[i].ObjectAlias))
                                {
                                    invList = invList + "|b" + _objs[i].ObjectName + "|xb";
                                }
                                else
                                {
                                    invList = invList + "|b" + _objs[i].ObjectAlias + "|xb";
                                }

                                if (!string.IsNullOrEmpty(_objs[i].Suffix))
                                {
                                    invList = invList + " " + _objs[i].Suffix;
                                }

                                invList = invList + ", ";
                            }
                        }
                    }
                    else
                    {
                        for (int j = 1, loopTo2 = _numberItems; j <= loopTo2; j++)
                        {
                            if (_items[j].Got == true)
                            {
                                invList = invList + _items[j].Name + ", ";
                            }
                        }
                    }
                    if (!string.IsNullOrEmpty(invList))
                    {

                        invList = Strings.Left(invList, Strings.Len(invList) - 2);
                        invList = Strings.UCase(Strings.Left(invList, 1)) + Strings.Mid(invList, 2);

                        int pos = 1;
                        int lastComma = default, thisComma;
                        do
                        {
                            thisComma = Strings.InStr(pos, invList, ",");
                            if (thisComma != 0)
                            {
                                lastComma = thisComma;
                                pos = thisComma + 1;
                            }
                        }
                        while (thisComma != 0);
                        if (lastComma != 0)
                            invList = Strings.Left(invList, lastComma - 1) + " and" + Strings.Mid(invList, lastComma + 1);
                        Print("You are carrying:|n" + invList + ".", ctx);
                    }
                    else
                    {
                        Print("You are not carrying anything.", ctx);
                    }
                }
                else if (CmdStartsWith(input, "oops "))
                {
                    ExecOops(GetEverythingAfter(input, "oops "), ctx);
                }
                else if (CmdStartsWith(input, "the "))
                {
                    ExecOops(GetEverythingAfter(input, "the "), ctx);
                }
                else
                {
                    PlayerErrorMessage(PlayerError.BadCommand, ctx);
                }
            }

            if (!skipAfterTurn)
            {
                // Execute any "afterturn" script:
                globalOverride = false;

                if (roomID != 0)
                {
                    if (!string.IsNullOrEmpty(_rooms[roomID].AfterTurnScript))
                    {
                        if (BeginsWith(_rooms[roomID].AfterTurnScript, "override"))
                        {
                            ExecuteScript(GetEverythingAfter(_rooms[roomID].AfterTurnScript, "override"), ctx);
                            globalOverride = true;
                        }
                        else
                        {
                            ExecuteScript(_rooms[roomID].AfterTurnScript, ctx);
                        }
                    }
                }

                // was set to NullThread here for some reason
                if (!string.IsNullOrEmpty(_afterTurnScript) & globalOverride == false)
                    ExecuteScript(_afterTurnScript, ctx);
            }

            Print("", ctx);

            if (!dontSetIt)
            {
                // Use "DontSetIt" when we don't want "it" etc. to refer to the object used in this turn.
                // This is used for e.g. auto-remove object from container when taking.
                _lastIt = _thisTurnIt;
                _lastItMode = _thisTurnItMode;
            }
            if ((_badCmdBefore ?? "") == (oldBadCmdBefore ?? ""))
                _badCmdBefore = "";

            return true;
        }

        private bool CmdStartsWith(string cmd, string startsWith)
        {
            // When we are checking user input in ExecCommand, we check things like whether
            // the player entered something beginning with "put ". We need to trim what the player entered
            // though, otherwise they might just type "put " and then we would try disambiguating an object
            // called "".

            return BeginsWith(Strings.Trim(cmd), startsWith);
        }

        private void ExecGive(string giveString, Context ctx)
        {
            string article;
            string item, character;
            Thing @type;
            var id = default(int);
            string script = "";
            int toPos = Strings.InStr(giveString, " to ");

            if (toPos == 0)
            {
                toPos = Strings.InStr(giveString, " the ");
                if (toPos == 0)
                {
                    PlayerErrorMessage(PlayerError.BadGive, ctx);
                    return;
                }
                else
                {
                    item = Strings.Trim(Strings.Mid(giveString, toPos + 4, Strings.Len(giveString) - (toPos + 2)));
                    character = Strings.Trim(Strings.Mid(giveString, 1, toPos));
                }
            }
            else
            {
                item = Strings.Trim(Strings.Mid(giveString, 1, toPos));
                character = Strings.Trim(Strings.Mid(giveString, toPos + 3, Strings.Len(giveString) - (toPos + 2)));
            }

            if (_gameAslVersion >= 281)
            {
                type = Thing.Object;
            }
            else
            {
                type = Thing.Character;
            }

            // First see if player has "ItemToGive":
            if (_gameAslVersion >= 280)
            {
                id = Disambiguate(item, "inventory", ctx);

                if (id == -1)
                {
                    PlayerErrorMessage(PlayerError.NoItem, ctx);
                    _badCmdBefore = "give";
                    _badCmdAfter = "to " + character;
                    return;
                }
                else if (id == -2)
                {
                    return;
                }
                else
                {
                    article = _objs[id].Article;
                }
            }
            else
            {
                // ASL2:
                bool notGot = true;

                for (int i = 1, loopTo = _numberItems; i <= loopTo; i++)
                {
                    if ((Strings.LCase(_items[i].Name) ?? "") == (Strings.LCase(item) ?? ""))
                    {
                        if (_items[i].Got == false)
                        {
                            notGot = true;
                            i = _numberItems;
                        }
                        else
                        {
                            notGot = false;
                        }
                    }
                }

                if (notGot == true)
                {
                    PlayerErrorMessage(PlayerError.NoItem, ctx);
                    return;
                }
                else
                {
                    article = _objs[id].Article;
                }
            }

            if (_gameAslVersion >= 281)
            {
                bool foundScript = false;
                bool foundObject = false;

                int giveToId = Disambiguate(character, _currentRoom, ctx);
                if (giveToId > 0)
                {
                    foundObject = true;
                }

                if (!foundObject)
                {
                    if (giveToId != -2)
                        PlayerErrorMessage(PlayerError.BadCharacter, ctx);
                    _badCmdBefore = "give " + item + " to";
                    return;
                }

                // Find appropriate give script ****
                // now, for "give a to b", we have
                // ItemID=a and GiveToObjectID=b

                var o = _objs[giveToId];

                for (int i = 1, loopTo1 = o.NumberGiveData; i <= loopTo1; i++)
                {
                    if (o.GiveData[i].GiveType == GiveType.GiveSomethingTo & (Strings.LCase(o.GiveData[i].GiveObject) ?? "") == (Strings.LCase(_objs[id].ObjectName) ?? ""))
                    {
                        foundScript = true;
                        script = o.GiveData[i].GiveScript;
                        break;
                    }
                }

                if (!foundScript)
                {
                    // check a for give to <b>:

                    var g = _objs[id];

                    for (int i = 1, loopTo2 = g.NumberGiveData; i <= loopTo2; i++)
                    {
                        if (g.GiveData[i].GiveType == GiveType.GiveToSomething & (Strings.LCase(g.GiveData[i].GiveObject) ?? "") == (Strings.LCase(_objs[giveToId].ObjectName) ?? ""))
                        {
                            foundScript = true;
                            script = g.GiveData[i].GiveScript;
                            break;
                        }
                    }
                }

                if (!foundScript)
                {
                    // check b for give anything:
                    script = _objs[giveToId].GiveAnything;
                    if (!string.IsNullOrEmpty(script))
                    {
                        foundScript = true;
                        SetStringContents("quest.give.object.name", _objs[id].ObjectName, ctx);
                    }
                }

                if (!foundScript)
                {
                    // check a for give to anything:
                    script = _objs[id].GiveToAnything;
                    if (!string.IsNullOrEmpty(script))
                    {
                        foundScript = true;
                        SetStringContents("quest.give.object.name", _objs[giveToId].ObjectName, ctx);
                    }
                }

                if (foundScript)
                {
                    ExecuteScript(script, ctx, id);
                }
                else
                {
                    SetStringContents("quest.error.charactername", _objs[giveToId].ObjectName, ctx);

                    string gender = Strings.Trim(_objs[giveToId].Gender);
                    gender = Strings.UCase(Strings.Left(gender, 1)) + Strings.Mid(gender, 2);
                    SetStringContents("quest.error.gender", gender, ctx);

                    SetStringContents("quest.error.article", article, ctx);
                    PlayerErrorMessage(PlayerError.ItemUnwanted, ctx);
                }
            }
            else
            {
                // ASL2:
                var block = GetThingBlock(character, _currentRoom, type);

                if (block.StartLine == 0 & block.EndLine == 0 | IsAvailable(character + "@" + _currentRoom, type, ctx) == false)
                {
                    PlayerErrorMessage(PlayerError.BadCharacter, ctx);
                    return;
                }

                string realName = _chars[GetThingNumber(character, _currentRoom, type)].ObjectName;

                // now, see if there's a give statement for this item in
                // this characters definition:

                int giveLine = 0;
                for (int i = block.StartLine + 1, loopTo3 = block.EndLine - 1; i <= loopTo3; i++)
                {
                    if (BeginsWith(_lines[i], "give"))
                    {
                        string ItemCheck = GetParameter(_lines[i], ctx);
                        if ((Strings.LCase(ItemCheck) ?? "") == (Strings.LCase(item) ?? ""))
                        {
                            giveLine = i;
                        }
                    }
                }

                if (giveLine == 0)
                {
                    if (string.IsNullOrEmpty(article))
                        article = "it";
                    SetStringContents("quest.error.charactername", realName, ctx);
                    SetStringContents("quest.error.gender", Strings.Trim(GetGender(character, true, ctx)), ctx);
                    SetStringContents("quest.error.article", article, ctx);
                    PlayerErrorMessage(PlayerError.ItemUnwanted, ctx);
                    return;
                }

                // now, execute the statement on GiveLine
                ExecuteScript(GetSecondChunk(_lines[giveLine]), ctx);
            }
        }

        private void ExecLook(string lookLine, Context ctx)
        {
            string item;

            if (Strings.Trim(lookLine) == "look")
            {
                ShowRoomInfo(_currentRoom, ctx);
            }
            else
            {
                if (_gameAslVersion < 391)
                {
                    int atPos = Strings.InStr(lookLine, " at ");

                    if (atPos == 0)
                    {
                        item = GetEverythingAfter(lookLine, "look ");
                    }
                    else
                    {
                        item = Strings.Trim(Strings.Mid(lookLine, atPos + 4));
                    }
                }
                else if (BeginsWith(lookLine, "look at "))
                {
                    item = GetEverythingAfter(lookLine, "look at ");
                }
                else if (BeginsWith(lookLine, "look in "))
                {
                    item = GetEverythingAfter(lookLine, "look in ");
                }
                else if (BeginsWith(lookLine, "look on "))
                {
                    item = GetEverythingAfter(lookLine, "look on ");
                }
                else if (BeginsWith(lookLine, "look inside "))
                {
                    item = GetEverythingAfter(lookLine, "look inside ");
                }
                else
                {
                    item = GetEverythingAfter(lookLine, "look ");
                }

                if (_gameAslVersion >= 280)
                {
                    int id = Disambiguate(item, "inventory;" + _currentRoom, ctx);

                    if (id <= 0)
                    {
                        if (id != -2)
                            PlayerErrorMessage(PlayerError.BadThing, ctx);
                        _badCmdBefore = "look at";
                        return;
                    }

                    DoLook(id, ctx);
                }
                else
                {
                    if (BeginsWith(item, "the "))
                    {
                        item = GetEverythingAfter(item, "the ");
                    }

                    lookLine = RetrLine("object", item, "look", ctx);

                    if (lookLine != "<unfound>")
                    {
                        // Check for availability
                        if (IsAvailable(item, Thing.Object, ctx) == false)
                        {
                            lookLine = "<unfound>";
                        }
                    }

                    if (lookLine == "<unfound>")
                    {
                        lookLine = RetrLine("character", item, "look", ctx);

                        if (lookLine != "<unfound>")
                        {
                            if (IsAvailable(item, Thing.Character, ctx) == false)
                            {
                                lookLine = "<unfound>";
                            }
                        }

                        if (lookLine == "<unfound>")
                        {
                            PlayerErrorMessage(PlayerError.BadThing, ctx);
                            return;
                        }
                        else if (lookLine == "<undefined>")
                        {
                            PlayerErrorMessage(PlayerError.DefaultLook, ctx);
                            return;
                        }
                    }
                    else if (lookLine == "<undefined>")
                    {
                        PlayerErrorMessage(PlayerError.DefaultLook, ctx);
                        return;
                    }

                    string lookData = Strings.Trim(GetEverythingAfter(Strings.Trim(lookLine), "look "));
                    if (Strings.Left(lookData, 1) == "<")
                    {
                        string LookText = GetParameter(lookLine, ctx);
                        Print(LookText, ctx);
                    }
                    else
                    {
                        ExecuteScript(lookData, ctx);
                    }
                }
            }

        }

        private void ExecSpeak(string cmd, Context ctx)
        {
            if (BeginsWith(cmd, "the "))
            {
                cmd = GetEverythingAfter(cmd, "the ");
            }

            string name = cmd;

            // if the "speak" parameter of the character c$'s definition
            // is just a parameter, say it - otherwise execute it as
            // a script.

            if (_gameAslVersion >= 281)
            {
                string speakLine = "";

                int ObjID = Disambiguate(name, "inventory;" + _currentRoom, ctx);
                if (ObjID <= 0)
                {
                    if (ObjID != -2)
                        PlayerErrorMessage(PlayerError.BadThing, ctx);
                    _badCmdBefore = "speak to";
                    return;
                }

                bool foundSpeak = false;

                // First look for action, then look
                // for property, then check define
                // section:

                var o = _objs[ObjID];

                for (int i = 1, loopTo = o.NumberActions; i <= loopTo; i++)
                {
                    if (o.Actions[i].ActionName == "speak")
                    {
                        speakLine = "speak " + o.Actions[i].Script;
                        foundSpeak = true;
                        break;
                    }
                }

                if (!foundSpeak)
                {
                    for (int i = 1, loopTo1 = o.NumberProperties; i <= loopTo1; i++)
                    {
                        if (o.Properties[i].PropertyName == "speak")
                        {
                            speakLine = "speak <" + o.Properties[i].PropertyValue + ">";
                            foundSpeak = true;
                            break;
                        }
                    }
                }

                // For some reason ASL3 < 311 looks for a "look" tag rather than
                // having had this set up at initialisation.
                if (_gameAslVersion < 311 & !foundSpeak)
                {
                    for (int i = o.DefinitionSectionStart, loopTo2 = o.DefinitionSectionEnd; i <= loopTo2; i++)
                    {
                        if (BeginsWith(_lines[i], "speak "))
                        {
                            speakLine = _lines[i];
                            foundSpeak = true;
                        }
                    }
                }

                if (!foundSpeak)
                {
                    SetStringContents("quest.error.gender", Strings.UCase(Strings.Left(_objs[ObjID].Gender, 1)) + Strings.Mid(_objs[ObjID].Gender, 2), ctx);
                    PlayerErrorMessage(PlayerError.DefaultSpeak, ctx);
                    return;
                }

                speakLine = GetEverythingAfter(speakLine, "speak ");

                if (BeginsWith(speakLine, "<"))
                {
                    string text = GetParameter(speakLine, ctx);
                    if (_gameAslVersion >= 350)
                    {
                        Print(text, ctx);
                    }
                    else
                    {
                        Print('"' + text + '"', ctx);
                    }
                }
                else
                {
                    ExecuteScript(speakLine, ctx, ObjID);
                }
            }

            else
            {
                string line = RetrLine("character", cmd, "speak", ctx);
                var @type = Thing.Character;

                string data = Strings.Trim(GetEverythingAfter(Strings.Trim(line), "speak "));

                if (line != "<unfound>" & line != "<undefined>")
                {
                    // Character exists; but is it available??
                    if (IsAvailable(cmd + "@" + _currentRoom, type, ctx) == false)
                    {
                        line = "<undefined>";
                    }
                }

                if (line == "<undefined>")
                {
                    PlayerErrorMessage(PlayerError.BadCharacter, ctx);
                }
                else if (line == "<unfound>")
                {
                    SetStringContents("quest.error.gender", Strings.Trim(GetGender(cmd, true, ctx)), ctx);
                    SetStringContents("quest.error.charactername", cmd, ctx);
                    PlayerErrorMessage(PlayerError.DefaultSpeak, ctx);
                }
                else if (BeginsWith(data, "<"))
                {
                    data = GetParameter(line, ctx);
                    Print('"' + data + '"', ctx);
                }
                else
                {
                    ExecuteScript(data, ctx);
                }
            }

        }

        private void ExecTake(string item, Context ctx)
        {
            var parentID = default(int);
            string parentDisplayName;
            bool foundItem = true;
            bool foundTake = false;
            int id = Disambiguate(item, _currentRoom, ctx);

            if (id < 0)
            {
                foundItem = false;
            }
            else
            {
                foundItem = true;
            }

            if (!foundItem)
            {
                if (id != -2)
                {
                    if (_gameAslVersion >= 410)
                    {
                        id = Disambiguate(item, "inventory", ctx);
                        if (id >= 0)
                        {
                            // Player already has this item
                            PlayerErrorMessage(PlayerError.AlreadyTaken, ctx);
                        }
                        else
                        {
                            PlayerErrorMessage(PlayerError.BadThing, ctx);
                        }
                    }
                    else if (_gameAslVersion >= 391)
                    {
                        PlayerErrorMessage(PlayerError.BadThing, ctx);
                    }
                    else
                    {
                        PlayerErrorMessage(PlayerError.BadItem, ctx);
                    }
                }
                _badCmdBefore = "take";
                return;
            }
            else
            {
                SetStringContents("quest.error.article", _objs[id].Article, ctx);
            }

            bool isInContainer = false;

            if (_gameAslVersion >= 391)
            {
                var canAccessObject = PlayerCanAccessObject(id);
                if (!canAccessObject.CanAccessObject)
                {
                    PlayerErrorMessage_ExtendInfo(PlayerError.BadTake, ctx, canAccessObject.ErrorMsg);
                    return;
                }

                string parent = GetObjectProperty("parent", id, false, false);
                parentID = GetObjectIdNoAlias(parent);
            }

            if (_gameAslVersion >= 280)
            {
                var t = _objs[id].Take;

                if (isInContainer & (t.Type == TextActionType.Default | t.Type == TextActionType.Text))
                {
                    // So, we want to take an object that's in a container or surface. So first
                    // we have to remove the object from that container.

                    if (!string.IsNullOrEmpty(_objs[parentID].ObjectAlias))
                    {
                        parentDisplayName = _objs[parentID].ObjectAlias;
                    }
                    else
                    {
                        parentDisplayName = _objs[parentID].ObjectName;
                    }

                    Print("(first removing " + _objs[id].Article + " from " + parentDisplayName + ")", ctx);

                    // Try to remove the object
                    ctx.AllowRealNamesInCommand = true;
                    ExecCommand("remove " + _objs[id].ObjectName, ctx, false, dontSetIt: true);

                    if (!string.IsNullOrEmpty(GetObjectProperty("parent", id, false, false)))
                    {
                        // removing the object failed
                        return;
                    }
                }

                if (t.Type == TextActionType.Default)
                {
                    PlayerErrorMessage(PlayerError.DefaultTake, ctx);
                    PlayerItem(item, true, ctx, id);
                }
                else if (t.Type == TextActionType.Text)
                {
                    Print(t.Data, ctx);
                    PlayerItem(item, true, ctx, id);
                }
                else if (t.Type == TextActionType.Script)
                {
                    ExecuteScript(t.Data, ctx, id);
                }
                else
                {
                    PlayerErrorMessage(PlayerError.BadTake, ctx);
                }
            }
            else
            {
                // find 'take' line
                for (int i = _objs[id].DefinitionSectionStart + 1, loopTo = _objs[id].DefinitionSectionEnd - 1; i <= loopTo; i++)
                {
                    if (BeginsWith(_lines[i], "take"))
                    {
                        string script = Strings.Trim(GetEverythingAfter(Strings.Trim(_lines[i]), "take"));
                        ExecuteScript(script, ctx, id);
                        foundTake = true;
                        i = _objs[id].DefinitionSectionEnd;
                    }
                }

                if (!foundTake)
                {
                    PlayerErrorMessage(PlayerError.BadTake, ctx);
                }
            }
        }

        private void ExecUse(string useLine, Context ctx)
        {
            int endOnWith;
            string useDeclareLine = "";
            string useOn, useItem;

            useLine = Strings.Trim(GetEverythingAfter(useLine, "use "));

            int roomId;
            roomId = GetRoomID(_currentRoom, ctx);

            int onWithPos = Strings.InStr(useLine, " on ");
            if (onWithPos == 0)
            {
                onWithPos = Strings.InStr(useLine, " with ");
                endOnWith = onWithPos + 4;
            }
            else
            {
                endOnWith = onWithPos + 2;
            }

            if (onWithPos != 0)
            {
                useOn = Strings.Trim(Strings.Right(useLine, Strings.Len(useLine) - endOnWith));
                useItem = Strings.Trim(Strings.Left(useLine, onWithPos - 1));
            }
            else
            {
                useOn = "";
                useItem = useLine;
            }

            // see if player has this item:

            var id = default(int);
            bool notGotItem;
            if (_gameAslVersion >= 280)
            {
                bool foundItem = false;

                id = Disambiguate(useItem, "inventory", ctx);
                if (id > 0)
                    foundItem = true;

                if (!foundItem)
                {
                    if (id != -2)
                        PlayerErrorMessage(PlayerError.NoItem, ctx);
                    if (string.IsNullOrEmpty(useOn))
                    {
                        _badCmdBefore = "use";
                    }
                    else
                    {
                        _badCmdBefore = "use";
                        _badCmdAfter = "on " + useOn;
                    }
                    return;
                }
            }
            else
            {
                notGotItem = true;

                for (int i = 1, loopTo = _numberItems; i <= loopTo; i++)
                {
                    if ((Strings.LCase(_items[i].Name) ?? "") == (Strings.LCase(useItem) ?? ""))
                    {
                        if (_items[i].Got == false)
                        {
                            notGotItem = true;
                            i = _numberItems;
                        }
                        else
                        {
                            notGotItem = false;
                        }
                    }
                }

                if (notGotItem == true)
                {
                    PlayerErrorMessage(PlayerError.NoItem, ctx);
                    return;
                }
            }

            string useScript = "";
            bool foundUseScript;
            bool foundUseOnObject;
            int useOnObjectId;
            bool found;
            if (_gameAslVersion >= 280)
            {
                foundUseScript = false;

                if (string.IsNullOrEmpty(useOn))
                {
                    if (_gameAslVersion < 410)
                    {
                        var r = _rooms[roomId];
                        for (int i = 1, loopTo1 = r.NumberUse; i <= loopTo1; i++)
                        {
                            if ((Strings.LCase(_objs[id].ObjectName) ?? "") == (Strings.LCase(r.Use[i].Text) ?? ""))
                            {
                                foundUseScript = true;
                                useScript = r.Use[i].Script;
                                break;
                            }
                        }
                    }

                    if (!foundUseScript)
                    {
                        useScript = _objs[id].Use;
                        if (!string.IsNullOrEmpty(useScript))
                            foundUseScript = true;
                    }
                }
                else
                {
                    foundUseOnObject = false;

                    useOnObjectId = Disambiguate(useOn, _currentRoom, ctx);
                    if (useOnObjectId > 0)
                    {
                        foundUseOnObject = true;
                    }
                    else
                    {
                        useOnObjectId = Disambiguate(useOn, "inventory", ctx);
                        if (useOnObjectId > 0)
                        {
                            foundUseOnObject = true;
                        }
                    }

                    if (!foundUseOnObject)
                    {
                        if (useOnObjectId != -2)
                            PlayerErrorMessage(PlayerError.BadThing, ctx);
                        _badCmdBefore = "use " + useItem + " on";
                        return;
                    }

                    // now, for "use a on b", we have
                    // ItemID=a and UseOnObjectID=b

                    // first check b for use <a>:

                    var o = _objs[useOnObjectId];

                    for (int i = 1, loopTo2 = o.NumberUseData; i <= loopTo2; i++)
                    {
                        if (o.UseData[i].UseType == UseType.UseSomethingOn & (Strings.LCase(o.UseData[i].UseObject) ?? "") == (Strings.LCase(_objs[id].ObjectName) ?? ""))
                        {
                            foundUseScript = true;
                            useScript = o.UseData[i].UseScript;
                            break;
                        }
                    }

                    if (!foundUseScript)
                    {
                        // check a for use on <b>:
                        var u = _objs[id];
                        for (int i = 1, loopTo3 = u.NumberUseData; i <= loopTo3; i++)
                        {
                            if (u.UseData[i].UseType == UseType.UseOnSomething & (Strings.LCase(u.UseData[i].UseObject) ?? "") == (Strings.LCase(_objs[useOnObjectId].ObjectName) ?? ""))
                            {
                                foundUseScript = true;
                                useScript = u.UseData[i].UseScript;
                                break;
                            }
                        }
                    }

                    if (!foundUseScript)
                    {
                        // check b for use anything:
                        useScript = _objs[useOnObjectId].UseAnything;
                        if (!string.IsNullOrEmpty(useScript))
                        {
                            foundUseScript = true;
                            SetStringContents("quest.use.object.name", _objs[id].ObjectName, ctx);
                        }
                    }

                    if (!foundUseScript)
                    {
                        // check a for use on anything:
                        useScript = _objs[id].UseOnAnything;
                        if (!string.IsNullOrEmpty(useScript))
                        {
                            foundUseScript = true;
                            SetStringContents("quest.use.object.name", _objs[useOnObjectId].ObjectName, ctx);
                        }
                    }
                }

                if (foundUseScript)
                {
                    ExecuteScript(useScript, ctx, id);
                }
                else
                {
                    PlayerErrorMessage(PlayerError.DefaultUse, ctx);
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(useOn))
                {
                    useDeclareLine = RetrLineParam("object", useOn, "use", useItem, ctx);
                }
                else
                {
                    found = false;
                    for (int i = 1, loopTo4 = _rooms[roomId].NumberUse; i <= loopTo4; i++)
                    {
                        if ((Strings.LCase(_rooms[roomId].Use[i].Text) ?? "") == (Strings.LCase(useItem) ?? ""))
                        {
                            useDeclareLine = "use <> " + _rooms[roomId].Use[i].Script;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        useDeclareLine = FindLine(GetDefineBlock("game"), "use", useItem);
                    }

                    if (!found & string.IsNullOrEmpty(useDeclareLine))
                    {
                        PlayerErrorMessage(PlayerError.DefaultUse, ctx);
                        return;
                    }
                }

                if (useDeclareLine != "<unfound>" & useDeclareLine != "<undefined>" & !string.IsNullOrEmpty(useOn))
                {
                    // Check for object availablity
                    if (IsAvailable(useOn, Thing.Object, ctx) == false)
                    {
                        useDeclareLine = "<undefined>";
                    }
                }

                if (useDeclareLine == "<undefined>")
                {
                    useDeclareLine = RetrLineParam("character", useOn, "use", useItem, ctx);

                    if (useDeclareLine != "<undefined>")
                    {
                        // Check for character availability
                        if (IsAvailable(useOn, Thing.Character, ctx) == false)
                        {
                            useDeclareLine = "<undefined>";
                        }
                    }

                    if (useDeclareLine == "<undefined>")
                    {
                        PlayerErrorMessage(PlayerError.BadThing, ctx);
                        return;
                    }
                    else if (useDeclareLine == "<unfound>")
                    {
                        PlayerErrorMessage(PlayerError.DefaultUse, ctx);
                        return;
                    }
                }
                else if (useDeclareLine == "<unfound>")
                {
                    PlayerErrorMessage(PlayerError.DefaultUse, ctx);
                    return;
                }

                string script = Strings.Right(useDeclareLine, Strings.Len(useDeclareLine) - Strings.InStr(useDeclareLine, ">"));
                ExecuteScript(script, ctx);
            }

        }

        private void ObjectActionUpdate(int id, string name, string script, bool noUpdate = false)
        {
            string objectName;
            int sp, ep;
            name = Strings.LCase(name);

            if (!noUpdate)
            {
                if (name == "take")
                {
                    _objs[id].Take.Data = script;
                    _objs[id].Take.Type = TextActionType.Script;
                }
                else if (name == "use")
                {
                    AddToUseInfo(id, script);
                }
                else if (name == "gain")
                {
                    _objs[id].GainScript = script;
                }
                else if (name == "lose")
                {
                    _objs[id].LoseScript = script;
                }
                else if (BeginsWith(name, "use "))
                {
                    name = GetEverythingAfter(name, "use ");
                    if (Strings.InStr(name, "'") > 0)
                    {
                        sp = Strings.InStr(name, "'");
                        ep = Strings.InStr(sp + 1, name, "'");
                        if (ep == 0)
                        {
                            LogASLError("Missing ' in 'action <use " + name + "> " + ReportErrorLine(script));
                            return;
                        }

                        objectName = Strings.Mid(name, sp + 1, ep - sp - 1);

                        AddToUseInfo(id, Strings.Trim(Strings.Left(name, sp - 1)) + " <" + objectName + "> " + script);
                    }
                    else
                    {
                        AddToUseInfo(id, name + " " + script);
                    }
                }
                else if (BeginsWith(name, "give "))
                {
                    name = GetEverythingAfter(name, "give ");
                    if (Strings.InStr(name, "'") > 0)
                    {

                        sp = Strings.InStr(name, "'");
                        ep = Strings.InStr(sp + 1, name, "'");
                        if (ep == 0)
                        {
                            LogASLError("Missing ' in 'action <give " + name + "> " + ReportErrorLine(script));
                            return;
                        }

                        objectName = Strings.Mid(name, sp + 1, ep - sp - 1);

                        AddToGiveInfo(id, Strings.Trim(Strings.Left(name, sp - 1)) + " <" + objectName + "> " + script);
                    }
                    else
                    {
                        AddToGiveInfo(id, name + " " + script);
                    }
                }
            }

            if (_gameFullyLoaded)
            {
                this.AddToObjectChangeLog(LegacyASL.ChangeLog.AppliesTo.Object, _objs[id].ObjectName, name, "action <" + name + "> " + script);
            }

        }

        private void ExecuteIf(string scriptLine, Context ctx)
        {
            string ifLine = Strings.Trim(GetEverythingAfter(Strings.Trim(scriptLine), "if "));
            string obscuredLine = ObliterateParameters(ifLine);
            int thenPos = Strings.InStr(obscuredLine, "then");

            if (thenPos == 0)
            {
                string errMsg = "Expected 'then' missing from script statement '" + ReportErrorLine(scriptLine) + "' - statement bypassed.";
                LogASLError(errMsg, LogType.WarningError);
                return;
            }

            string conditions = Strings.Trim(Strings.Left(ifLine, thenPos - 1));

            thenPos = thenPos + 4;
            int elsePos = Strings.InStr(obscuredLine, "else");
            int thenEndPos;

            if (elsePos == 0)
            {
                thenEndPos = Strings.Len(obscuredLine) + 1;
            }
            else
            {
                thenEndPos = elsePos - 1;
            }

            string thenScript = Strings.Trim(Strings.Mid(ifLine, thenPos, thenEndPos - thenPos));
            string elseScript = "";

            if (elsePos != 0)
            {
                elseScript = Strings.Trim(Strings.Right(ifLine, Strings.Len(ifLine) - (thenEndPos + 4)));
            }

            // Remove braces from around "then" and "else" script
            // commands, if present
            if (Strings.Left(thenScript, 1) == "{" & Strings.Right(thenScript, 1) == "}")
            {
                thenScript = Strings.Mid(thenScript, 2, Strings.Len(thenScript) - 2);
            }
            if (Strings.Left(elseScript, 1) == "{" & Strings.Right(elseScript, 1) == "}")
            {
                elseScript = Strings.Mid(elseScript, 2, Strings.Len(elseScript) - 2);
            }

            if (ExecuteConditions(conditions, ctx))
            {
                ExecuteScript(thenScript, ctx);
            }
            else if (elsePos != 0)
                ExecuteScript(elseScript, ctx);

        }

        private void ExecuteScript(string scriptLine, Context ctx, int newCallingObjectId = 0)
        {
            try
            {
                if (string.IsNullOrEmpty(Strings.Trim(scriptLine)))
                    return;
                if (_gameFinished)
                    return;

                if (Strings.InStr(scriptLine, Constants.vbCrLf) > 0)
                {
                    int curPos = 1;
                    bool finished = false;
                    do
                    {
                        int crLfPos = Strings.InStr(curPos, scriptLine, Constants.vbCrLf);
                        if (crLfPos == 0)
                        {
                            finished = true;
                            crLfPos = Strings.Len(scriptLine) + 1;
                        }

                        string curScriptLine = Strings.Trim(Strings.Mid(scriptLine, curPos, crLfPos - curPos));
                        if ((curScriptLine ?? "") != Constants.vbCrLf)
                        {
                            ExecuteScript(curScriptLine, ctx);
                        }
                        curPos = crLfPos + 2;
                    }
                    while (!finished);
                    return;
                }

                if (newCallingObjectId != 0)
                {
                    ctx.CallingObjectId = newCallingObjectId;
                }

                if (BeginsWith(scriptLine, "if "))
                {
                    ExecuteIf(scriptLine, ctx);
                }
                else if (BeginsWith(scriptLine, "select case "))
                {
                    ExecuteSelectCase(scriptLine, ctx);
                }
                else if (BeginsWith(scriptLine, "choose "))
                {
                    ExecuteChoose(GetParameter(scriptLine, ctx), ctx);
                }
                else if (BeginsWith(scriptLine, "set "))
                {
                    ExecuteSet(GetEverythingAfter(scriptLine, "set "), ctx);
                }
                else if (BeginsWith(scriptLine, "inc ") | BeginsWith(scriptLine, "dec "))
                {
                    ExecuteIncDec(scriptLine, ctx);
                }
                else if (BeginsWith(scriptLine, "say "))
                {
                    Print('"' + GetParameter(scriptLine, ctx) + '"', ctx);
                }
                else if (BeginsWith(scriptLine, "do "))
                {
                    ExecuteDo(GetParameter(scriptLine, ctx), ctx);
                }
                else if (BeginsWith(scriptLine, "doaction "))
                {
                    ExecuteDoAction(GetParameter(scriptLine, ctx), ctx);
                }
                else if (BeginsWith(scriptLine, "give "))
                {
                    PlayerItem(GetParameter(scriptLine, ctx), true, ctx);
                }
                else if (BeginsWith(scriptLine, "lose ") | BeginsWith(scriptLine, "drop "))
                {
                    PlayerItem(GetParameter(scriptLine, ctx), false, ctx);
                }
                else if (BeginsWith(scriptLine, "msg "))
                {
                    Print(GetParameter(scriptLine, ctx), ctx);
                }
                else if (BeginsWith(scriptLine, "speak "))
                {
                    Speak(GetParameter(scriptLine, ctx));
                }
                else if (BeginsWith(scriptLine, "helpmsg "))
                {
                    Print(GetParameter(scriptLine, ctx), ctx);
                }
                else if (Strings.Trim(Strings.LCase(scriptLine)) == "helpclose")
                {
                }
                // This command does nothing in the Quest 5 player, as there is no separate help window
                else if (BeginsWith(scriptLine, "goto "))
                {
                    PlayGame(GetParameter(scriptLine, ctx), ctx);
                }
                else if (BeginsWith(scriptLine, "playerwin"))
                {
                    FinishGame(StopType.Win, ctx);
                }
                else if (BeginsWith(scriptLine, "playerlose"))
                {
                    FinishGame(StopType.Lose, ctx);
                }
                else if (Strings.Trim(Strings.LCase(scriptLine)) == "stop")
                {
                    FinishGame(StopType.Null, ctx);
                }
                else if (BeginsWith(scriptLine, "playwav "))
                {
                    PlayWav(GetParameter(scriptLine, ctx));
                }
                else if (BeginsWith(scriptLine, "playmidi "))
                {
                    PlayMedia(GetParameter(scriptLine, ctx));
                }
                else if (BeginsWith(scriptLine, "playmp3 "))
                {
                    PlayMedia(GetParameter(scriptLine, ctx));
                }
                else if (Strings.Trim(Strings.LCase(scriptLine)) == "picture close")
                {
                }
                // This command does nothing in the Quest 5 player, as there is no separate picture window
                else if (_gameAslVersion >= 390 & BeginsWith(scriptLine, "picture popup ") | _gameAslVersion >= 282 & _gameAslVersion < 390 & BeginsWith(scriptLine, "picture ") | _gameAslVersion < 282 & BeginsWith(scriptLine, "show "))
                {
                    ShowPicture(GetParameter(scriptLine, ctx));
                }
                else if (_gameAslVersion >= 390 & BeginsWith(scriptLine, "picture "))
                {
                    ShowPictureInText(GetParameter(scriptLine, ctx));
                }
                else if (BeginsWith(scriptLine, "animate persist "))
                {
                    ShowPicture(GetParameter(scriptLine, ctx));
                }
                else if (BeginsWith(scriptLine, "animate "))
                {
                    ShowPicture(GetParameter(scriptLine, ctx));
                }
                else if (BeginsWith(scriptLine, "extract "))
                {
                    ExtractFile(GetParameter(scriptLine, ctx));
                }
                else if (_gameAslVersion < 281 & BeginsWith(scriptLine, "hideobject "))
                {
                    SetAvailability(GetParameter(scriptLine, ctx), false, ctx);
                }
                else if (_gameAslVersion < 281 & BeginsWith(scriptLine, "showobject "))
                {
                    SetAvailability(GetParameter(scriptLine, ctx), true, ctx);
                }
                else if (_gameAslVersion < 281 & BeginsWith(scriptLine, "moveobject "))
                {
                    ExecMoveThing(GetParameter(scriptLine, ctx), Thing.Object, ctx);
                }
                else if (_gameAslVersion < 281 & BeginsWith(scriptLine, "hidechar "))
                {
                    SetAvailability(GetParameter(scriptLine, ctx), false, ctx, Thing.Character);
                }
                else if (_gameAslVersion < 281 & BeginsWith(scriptLine, "showchar "))
                {
                    SetAvailability(GetParameter(scriptLine, ctx), true, ctx, Thing.Character);
                }
                else if (_gameAslVersion < 281 & BeginsWith(scriptLine, "movechar "))
                {
                    ExecMoveThing(GetParameter(scriptLine, ctx), Thing.Character, ctx);
                }
                else if (_gameAslVersion < 281 & BeginsWith(scriptLine, "revealobject "))
                {
                    SetVisibility(GetParameter(scriptLine, ctx), Thing.Object, true, ctx);
                }
                else if (_gameAslVersion < 281 & BeginsWith(scriptLine, "concealobject "))
                {
                    SetVisibility(GetParameter(scriptLine, ctx), Thing.Object, false, ctx);
                }
                else if (_gameAslVersion < 281 & BeginsWith(scriptLine, "revealchar "))
                {
                    SetVisibility(GetParameter(scriptLine, ctx), Thing.Character, true, ctx);
                }
                else if (_gameAslVersion < 281 & BeginsWith(scriptLine, "concealchar "))
                {
                    SetVisibility(GetParameter(scriptLine, ctx), Thing.Character, false, ctx);
                }
                else if (_gameAslVersion >= 281 & BeginsWith(scriptLine, "hide "))
                {
                    SetAvailability(GetParameter(scriptLine, ctx), false, ctx);
                }
                else if (_gameAslVersion >= 281 & BeginsWith(scriptLine, "show "))
                {
                    SetAvailability(GetParameter(scriptLine, ctx), true, ctx);
                }
                else if (_gameAslVersion >= 281 & BeginsWith(scriptLine, "move "))
                {
                    ExecMoveThing(GetParameter(scriptLine, ctx), Thing.Object, ctx);
                }
                else if (_gameAslVersion >= 281 & BeginsWith(scriptLine, "reveal "))
                {
                    SetVisibility(GetParameter(scriptLine, ctx), Thing.Object, true, ctx);
                }
                else if (_gameAslVersion >= 281 & BeginsWith(scriptLine, "conceal "))
                {
                    SetVisibility(GetParameter(scriptLine, ctx), Thing.Object, false, ctx);
                }
                else if (_gameAslVersion >= 391 & BeginsWith(scriptLine, "open "))
                {
                    SetOpenClose(GetParameter(scriptLine, ctx), true, ctx);
                }
                else if (_gameAslVersion >= 391 & BeginsWith(scriptLine, "close "))
                {
                    SetOpenClose(GetParameter(scriptLine, ctx), false, ctx);
                }
                else if (_gameAslVersion >= 391 & BeginsWith(scriptLine, "add "))
                {
                    ExecAddRemoveScript(GetParameter(scriptLine, ctx), true, ctx);
                }
                else if (_gameAslVersion >= 391 & BeginsWith(scriptLine, "remove "))
                {
                    ExecAddRemoveScript(GetParameter(scriptLine, ctx), false, ctx);
                }
                else if (BeginsWith(scriptLine, "clone "))
                {
                    ExecClone(GetParameter(scriptLine, ctx), ctx);
                }
                else if (BeginsWith(scriptLine, "exec "))
                {
                    ExecExec(scriptLine, ctx);
                }
                else if (BeginsWith(scriptLine, "setstring "))
                {
                    ExecSetString(GetParameter(scriptLine, ctx), ctx);
                }
                else if (BeginsWith(scriptLine, "setvar "))
                {
                    ExecSetVar(GetParameter(scriptLine, ctx), ctx);
                }
                else if (BeginsWith(scriptLine, "for "))
                {
                    ExecFor(GetEverythingAfter(scriptLine, "for "), ctx);
                }
                else if (BeginsWith(scriptLine, "property "))
                {
                    ExecProperty(GetParameter(scriptLine, ctx), ctx);
                }
                else if (BeginsWith(scriptLine, "type "))
                {
                    ExecType(GetParameter(scriptLine, ctx), ctx);
                }
                else if (BeginsWith(scriptLine, "action "))
                {
                    ExecuteAction(GetEverythingAfter(scriptLine, "action "), ctx);
                }
                else if (BeginsWith(scriptLine, "flag "))
                {
                    ExecuteFlag(GetEverythingAfter(scriptLine, "flag "), ctx);
                }
                else if (BeginsWith(scriptLine, "create "))
                {
                    ExecuteCreate(GetEverythingAfter(scriptLine, "create "), ctx);
                }
                else if (BeginsWith(scriptLine, "destroy exit "))
                {
                    DestroyExit(GetParameter(scriptLine, ctx), ctx);
                }
                else if (BeginsWith(scriptLine, "repeat "))
                {
                    ExecuteRepeat(GetEverythingAfter(scriptLine, "repeat "), ctx);
                }
                else if (BeginsWith(scriptLine, "enter "))
                {
                    ExecuteEnter(scriptLine, ctx);
                }
                else if (BeginsWith(scriptLine, "displaytext "))
                {
                    DisplayTextSection(GetParameter(scriptLine, ctx), ctx);
                }
                else if (BeginsWith(scriptLine, "helpdisplaytext "))
                {
                    DisplayTextSection(GetParameter(scriptLine, ctx), ctx);
                }
                else if (BeginsWith(scriptLine, "font "))
                {
                    SetFont(GetParameter(scriptLine, ctx));
                }
                else if (BeginsWith(scriptLine, "pause "))
                {
                    Pause(Conversions.ToInteger(GetParameter(scriptLine, ctx)));
                }
                else if (Strings.Trim(Strings.LCase(scriptLine)) == "clear")
                {
                    DoClear();
                }
                else if (Strings.Trim(Strings.LCase(scriptLine)) == "helpclear")
                {
                }
                // This command does nothing in the Quest 5 player, as there is no separate help window
                else if (BeginsWith(scriptLine, "background "))
                {
                    SetBackground(GetParameter(scriptLine, ctx));
                }
                else if (BeginsWith(scriptLine, "foreground "))
                {
                    SetForeground(GetParameter(scriptLine, ctx));
                }
                else if (Strings.Trim(Strings.LCase(scriptLine)) == "nointro")
                {
                    _autoIntro = false;
                }
                else if (BeginsWith(scriptLine, "debug "))
                {
                    LogASLError(GetParameter(scriptLine, ctx), LogType.Misc);
                }
                else if (BeginsWith(scriptLine, "mailto "))
                {
                    string emailAddress = GetParameter(scriptLine, ctx);
                    PrintText?.Invoke("<a target=\"_blank\" href=\"mailto:" + emailAddress + "\">" + emailAddress + "</a>");
                }
                else if (BeginsWith(scriptLine, "shell ") & _gameAslVersion < 410)
                {
                    LogASLError("'shell' is not supported in this version of Quest", LogType.WarningError);
                }
                else if (BeginsWith(scriptLine, "shellexe ") & _gameAslVersion < 410)
                {
                    LogASLError("'shellexe' is not supported in this version of Quest", LogType.WarningError);
                }
                else if (BeginsWith(scriptLine, "wait"))
                {
                    ExecuteWait(Strings.Trim(GetEverythingAfter(Strings.Trim(scriptLine), "wait")), ctx);
                }
                else if (BeginsWith(scriptLine, "timeron "))
                {
                    SetTimerState(GetParameter(scriptLine, ctx), true);
                }
                else if (BeginsWith(scriptLine, "timeroff "))
                {
                    SetTimerState(GetParameter(scriptLine, ctx), false);
                }
                else if (Strings.Trim(Strings.LCase(scriptLine)) == "outputon")
                {
                    _outPutOn = true;
                    UpdateObjectList(ctx);
                    UpdateItems(ctx);
                }
                else if (Strings.Trim(Strings.LCase(scriptLine)) == "outputoff")
                {
                    _outPutOn = false;
                }
                else if (Strings.Trim(Strings.LCase(scriptLine)) == "panes off")
                {
                    _player.SetPanesVisible("off");
                }
                else if (Strings.Trim(Strings.LCase(scriptLine)) == "panes on")
                {
                    _player.SetPanesVisible("on");
                }
                else if (BeginsWith(scriptLine, "lock "))
                {
                    ExecuteLock(GetParameter(scriptLine, ctx), true);
                }
                else if (BeginsWith(scriptLine, "unlock "))
                {
                    ExecuteLock(GetParameter(scriptLine, ctx), false);
                }
                else if (BeginsWith(scriptLine, "playmod ") & _gameAslVersion < 410)
                {
                    LogASLError("'playmod' is not supported in this version of Quest", LogType.WarningError);
                }
                else if (BeginsWith(scriptLine, "modvolume") & _gameAslVersion < 410)
                {
                    LogASLError("'modvolume' is not supported in this version of Quest", LogType.WarningError);
                }
                else if (Strings.Trim(Strings.LCase(scriptLine)) == "dontprocess")
                {
                    ctx.DontProcessCommand = true;
                }
                else if (BeginsWith(scriptLine, "return "))
                {
                    ctx.FunctionReturnValue = GetParameter(scriptLine, ctx);
                }
                else if (BeginsWith(scriptLine, "'") == false)
                {
                    LogASLError("Unrecognized keyword. Line reads: '" + Strings.Trim(ReportErrorLine(scriptLine)) + "'", LogType.WarningError);
                }
            }
            catch
            {
                Print("[An internal error occurred]", ctx);
                LogASLError(Information.Err().Number + " - '" + Information.Err().Description + "' occurred processing script line '" + scriptLine + "'", LogType.InternalError);
            }
        }

        private void ExecuteEnter(string scriptLine, Context ctx)
        {
            _commandOverrideModeOn = true;
            _commandOverrideVariable = GetParameter(scriptLine, ctx);

            // Now, wait for CommandOverrideModeOn to be set
            // to False by ExecCommand. Execution can then resume.

            ChangeState(State.Waiting, true);

            lock (_commandLock)
                System.Threading.Monitor.Wait(_commandLock);

            _commandOverrideModeOn = false;

            // State will have been changed to Working when the user typed their response,
            // and will be set back to Ready when the call to ExecCommand has finished
        }

        private void ExecuteSet(string setInstruction, Context ctx)
        {
            if (_gameAslVersion >= 280)
            {
                if (BeginsWith(setInstruction, "interval "))
                {
                    string interval = GetParameter(setInstruction, ctx);
                    int scp = Strings.InStr(interval, ";");
                    if (scp == 0)
                    {
                        LogASLError("Too few parameters in 'set " + setInstruction + "'", LogType.WarningError);
                        return;
                    }

                    string name = Strings.Trim(Strings.Left(interval, scp - 1));
                    interval = Conversion.Val(Strings.Trim(Strings.Mid(interval, scp + 1))).ToString();
                    bool found = false;

                    for (int i = 1, loopTo = _numberTimers; i <= loopTo; i++)
                    {
                        if ((Strings.LCase(name) ?? "") == (Strings.LCase(_timers[i].TimerName) ?? ""))
                        {
                            found = true;
                            _timers[i].TimerInterval = Conversions.ToInteger(interval);
                            i = _numberTimers;
                        }
                    }

                    if (!found)
                    {
                        LogASLError("No such timer '" + name + "'", LogType.WarningError);
                        return;
                    }
                }
                else if (BeginsWith(setInstruction, "string "))
                {
                    ExecSetString(GetParameter(setInstruction, ctx), ctx);
                }
                else if (BeginsWith(setInstruction, "numeric "))
                {
                    ExecSetVar(GetParameter(setInstruction, ctx), ctx);
                }
                else if (BeginsWith(setInstruction, "collectable "))
                {
                    ExecuteSetCollectable(GetParameter(setInstruction, ctx), ctx);
                }
                else
                {
                    var result = SetUnknownVariableType(GetParameter(setInstruction, ctx), ctx);
                    if (result == SetResult.Error)
                    {
                        LogASLError("Error on setting 'set " + setInstruction + "'", LogType.WarningError);
                    }
                    else if (result == SetResult.Unfound)
                    {
                        LogASLError("Variable type not specified in 'set " + setInstruction + "'", LogType.WarningError);
                    }
                }
            }
            else
            {
                ExecuteSetCollectable(GetParameter(setInstruction, ctx), ctx);
            }

        }

        private string FindStatement(DefineBlock block, string statement)
        {
            // Finds a statement within a given block of lines

            for (int i = block.StartLine + 1, loopTo = block.EndLine - 1; i <= loopTo; i++)
            {

                // Ignore sub-define blocks
                if (BeginsWith(_lines[i], "define "))
                {
                    do
                        i = i + 1;
                    while (Strings.Trim(_lines[i]) != "end define");
                }
                // Check to see if the line matches the statement
                // that is begin searched for
                if (BeginsWith(_lines[i], statement))
                {
                    // Return the parameters between < and > :
                    return GetParameter(_lines[i], _nullContext);
                }
            }

            return "";
        }

        private string FindLine(DefineBlock block, string statement, string statementParam)
        {
            // Finds a statement within a given block of lines

            for (int i = block.StartLine + 1, loopTo = block.EndLine - 1; i <= loopTo; i++)
            {

                // Ignore sub-define blocks
                if (BeginsWith(_lines[i], "define "))
                {
                    do
                        i = i + 1;
                    while (Strings.Trim(_lines[i]) != "end define");
                }
                // Check to see if the line matches the statement
                // that is begin searched for
                if (BeginsWith(_lines[i], statement))
                {
                    if ((Strings.UCase(Strings.Trim(GetParameter(_lines[i], _nullContext))) ?? "") == (Strings.UCase(Strings.Trim(statementParam)) ?? ""))
                    {
                        return Strings.Trim(_lines[i]);
                    }
                }
            }

            return "";
        }

        private double GetCollectableAmount(string name)
        {
            for (int i = 1, loopTo = _numCollectables; i <= loopTo; i++)
            {
                if ((_collectables[i].Name ?? "") == (name ?? ""))
                {
                    return _collectables[i].Value;
                }
            }

            return 0d;
        }

        private string GetSecondChunk(string line)
        {
            int endOfFirstBit = Strings.InStr(line, ">") + 1;
            int lengthOfKeyword = Strings.Len(line) - endOfFirstBit + 1;
            return Strings.Trim(Strings.Mid(line, endOfFirstBit, lengthOfKeyword));
        }

        private void GoDirection(string direction, Context ctx)
        {
            // leaves the current room in direction specified by
            // 'direction'

            var dirData = new TextAction();
            int id = GetRoomID(_currentRoom, ctx);

            if (id == 0)
                return;

            if (_gameAslVersion >= 410)
            {
                _rooms[id].Exits.ExecuteGo(direction, ref ctx);
                return;
            }

            var r = _rooms[id];

            if (direction == "north")
            {
                dirData = r.North;
            }
            else if (direction == "south")
            {
                dirData = r.South;
            }
            else if (direction == "west")
            {
                dirData = r.West;
            }
            else if (direction == "east")
            {
                dirData = r.East;
            }
            else if (direction == "northeast")
            {
                dirData = r.NorthEast;
            }
            else if (direction == "northwest")
            {
                dirData = r.NorthWest;
            }
            else if (direction == "southeast")
            {
                dirData = r.SouthEast;
            }
            else if (direction == "southwest")
            {
                dirData = r.SouthWest;
            }
            else if (direction == "up")
            {
                dirData = r.Up;
            }
            else if (direction == "down")
            {
                dirData = r.Down;
            }
            else if (direction == "out")
            {
                if (string.IsNullOrEmpty(r.Out.Script))
                {
                    dirData.Data = r.Out.Text;
                    dirData.Type = TextActionType.Text;
                }
                else
                {
                    dirData.Data = r.Out.Script;
                    dirData.Type = TextActionType.Script;
                }
            }

            if (dirData.Type == TextActionType.Script & !string.IsNullOrEmpty(dirData.Data))
            {
                ExecuteScript(dirData.Data, ctx);
            }
            else if (!string.IsNullOrEmpty(dirData.Data))
            {
                string newRoom = dirData.Data;
                int scp = Strings.InStr(newRoom, ";");
                if (scp != 0)
                {
                    newRoom = Strings.Trim(Strings.Mid(newRoom, scp + 1));
                }
                PlayGame(newRoom, ctx);
            }
            else if (direction == "out")
            {
                PlayerErrorMessage(PlayerError.DefaultOut, ctx);
            }
            else
            {
                PlayerErrorMessage(PlayerError.BadPlace, ctx);
            }

        }

        private void GoToPlace(string place, Context ctx)
        {
            // leaves the current room in direction specified by
            // 'direction'

            string destination = "";
            string placeData;
            bool disallowed = false;

            placeData = PlaceExist(place, ctx);

            if (!string.IsNullOrEmpty(placeData))
            {
                destination = placeData;
            }
            else if (BeginsWith(place, "the "))
            {
                string np = GetEverythingAfter(place, "the ");
                placeData = PlaceExist(np, ctx);
                if (!string.IsNullOrEmpty(placeData))
                {
                    destination = placeData;
                }
                else
                {
                    disallowed = true;
                }
            }
            else
            {
                disallowed = true;
            }

            if (!string.IsNullOrEmpty(destination))
            {
                if (Strings.InStr(destination, ";") > 0)
                {
                    string s = Strings.Trim(Strings.Right(destination, Strings.Len(destination) - Strings.InStr(destination, ";")));
                    ExecuteScript(s, ctx);
                }
                else
                {
                    PlayGame(destination, ctx);
                }
            }

            if (disallowed == true)
            {
                PlayerErrorMessage(PlayerError.BadPlace, ctx);
            }
        }

        private async Task<bool> InitialiseGame(IGameData gameData, bool fromQsg = false)
        {
            _loadedFromQsg = fromQsg;

            _changeLogRooms = new LegacyASL.ChangeLog();
            _changeLogObjects = new LegacyASL.ChangeLog();
            _changeLogRooms.AppliesToType = LegacyASL.ChangeLog.AppliesTo.Room;
            _changeLogObjects.AppliesToType = LegacyASL.ChangeLog.AppliesTo.Object;

            _outPutOn = true;
            _useAbbreviations = true;

            // TODO: ?
            // _gamePath = System.IO.Path.GetDirectoryName(filename) + "\"

            LogASLError("Opening file " + gameData.Filename + " on " + DateTime.Now.ToString(), LogType.Init);

            // Parse file and find where the 'define' blocks are:
            if (await ParseFile(gameData) == false)
            {
                LogASLError("Unable to open file", LogType.Init);
                string err = "Unable to open " + gameData.Filename;

                if (!string.IsNullOrEmpty(_openErrorReport))
                {
                    // Strip last vbcrlf
                    _openErrorReport = Strings.Left(_openErrorReport, Strings.Len(_openErrorReport) - 2);
                    err = err + ":" + Constants.vbCrLf + Constants.vbCrLf + _openErrorReport;
                }

                Print("Error: " + err, _nullContext);
                return false;
            }

            // Check version
            DefineBlock gameBlock;
            gameBlock = GetDefineBlock("game");

            string aslVersion = "//";
            for (int i = gameBlock.StartLine + 1, loopTo = gameBlock.EndLine - 1; i <= loopTo; i++)
            {
                if (BeginsWith(_lines[i], "asl-version "))
                {
                    aslVersion = GetParameter(_lines[i], _nullContext);
                }
            }

            if (aslVersion == "//")
            {
                LogASLError("File contains no version header.", LogType.WarningError);
            }
            else
            {
                _gameAslVersion = Conversions.ToInteger(aslVersion);

                string recognisedVersions = "/100/200/210/217/280/281/282/283/284/285/300/310/311/320/350/390/391/392/400/410/";

                if (Strings.InStr(recognisedVersions, "/" + aslVersion + "/") == 0)
                {
                    LogASLError("Unrecognised ASL version number.", LogType.WarningError);
                }
            }

            _listVerbs.Add(ListType.ExitsList, new List<string>(new string[] { "Go to" }));

            if (_gameAslVersion >= 280 & _gameAslVersion < 390)
            {
                _listVerbs.Add(ListType.ObjectsList, new List<string>(new string[] { "Look at", "Examine", "Take", "Speak to" }));
                _listVerbs.Add(ListType.InventoryList, new List<string>(new string[] { "Look at", "Examine", "Use", "Drop" }));
            }
            else
            {
                _listVerbs.Add(ListType.ObjectsList, new List<string>(new string[] { "Look at", "Take", "Speak to" }));
                _listVerbs.Add(ListType.InventoryList, new List<string>(new string[] { "Look at", "Use", "Drop" }));
            }

            // Get the name of the game:
            _gameName = GetParameter(_lines[GetDefineBlock("game").StartLine], _nullContext);

            _player.UpdateGameName(_gameName);
            _player.Show("Panes");
            _player.Show("Location");
            _player.Show("Command");

            SetUpGameObject();
            SetUpOptions();

            for (int i = GetDefineBlock("game").StartLine + 1, loopTo1 = GetDefineBlock("game").EndLine - 1; i <= loopTo1; i++)
            {
                if (BeginsWith(_lines[i], "beforesave "))
                {
                    _beforeSaveScript = GetEverythingAfter(_lines[i], "beforesave ");
                }
                else if (BeginsWith(_lines[i], "onload "))
                {
                    _onLoadScript = GetEverythingAfter(_lines[i], "onload ");
                }
            }

            SetDefaultPlayerErrorMessages();

            SetUpSynonyms();
            SetUpRoomData();

            if (_gameAslVersion >= 410)
            {
                SetUpExits();
            }

            if (_gameAslVersion < 280)
            {
                // Set up an array containing the names of all the items
                // used in the game, based on the possitems statement
                // of the 'define game' block.
                SetUpItemArrays();
            }

            if (_gameAslVersion < 280)
            {
                // Now, go through the 'startitems' statement and set up
                // the items array so we start with those items mentioned.
                SetUpStartItems();
            }

            // Set up collectables.
            SetUpCollectables();

            SetUpDisplayVariables();

            // Set up characters and objects.
            SetUpCharObjectInfo();
            SetUpUserDefinedPlayerErrors();
            SetUpDefaultFonts();
            SetUpTurnScript();
            SetUpTimers();
            SetUpMenus();

            _gameFileName = gameData.Filename;

            LogASLError("Finished loading file.", LogType.Init);

            _defaultRoomProperties = GetPropertiesInType("defaultroom", false);
            _defaultProperties = GetPropertiesInType("default", false);

            return true;
        }

        private string PlaceExist(string placeName, Context ctx)
        {
            // Returns actual name of an available "place" exit, and if
            // script is executed on going in that direction, that script
            // is returned after a ";"

            int roomId = GetRoomID(_currentRoom, ctx);

            // check if place is available
            var r = _rooms[roomId];

            for (int i = 1, loopTo = r.NumberPlaces; i <= loopTo; i++)
            {
                string checkPlace = r.Places[i].PlaceName;

                // remove any prefix and semicolon
                if (Strings.InStr(checkPlace, ";") > 0)
                {
                    checkPlace = Strings.Trim(Strings.Right(checkPlace, Strings.Len(checkPlace) - (Strings.InStr(checkPlace, ";") + 1)));
                }

                string checkPlaceName = checkPlace;

                if (_gameAslVersion >= 311 & string.IsNullOrEmpty(r.Places[i].Script))
                {
                    int destRoomId = GetRoomID(checkPlace, ctx);
                    if (destRoomId != 0)
                    {
                        if (!string.IsNullOrEmpty(_rooms[destRoomId].RoomAlias))
                        {
                            checkPlaceName = _rooms[destRoomId].RoomAlias;
                        }
                    }
                }

                if ((Strings.LCase(checkPlaceName) ?? "") == (Strings.LCase(placeName) ?? ""))
                {
                    if (!string.IsNullOrEmpty(r.Places[i].Script))
                    {
                        return checkPlace + ";" + r.Places[i].Script;
                    }
                    else
                    {
                        return checkPlace;
                    }
                }
            }

            return "";
        }

        private void PlayerItem(string item, bool got, Context ctx, int objId = 0)
        {
            // Gives the player an item (if got=True) or takes an
            // item away from the player (if got=False).

            // If ASL>280, setting got=TRUE moves specified
            // *object* to room "inventory"; setting got=FALSE
            // drops object into current room.

            bool foundObjectName = false;

            if (_gameAslVersion >= 280)
            {
                if (objId == 0)
                {
                    for (int i = 1, loopTo = _numberObjs; i <= loopTo; i++)
                    {
                        if ((Strings.LCase(_objs[i].ObjectName) ?? "") == (Strings.LCase(item) ?? ""))
                        {
                            objId = i;
                            break;
                        }
                    }
                }

                if (objId != 0)
                {
                    if (got)
                    {
                        if (_gameAslVersion >= 391)
                        {
                            // Unset parent information, if any
                            AddToObjectProperties("not parent", objId, ctx);
                        }
                        MoveThing(_objs[objId].ObjectName, "inventory", Thing.Object, ctx);

                        if (!string.IsNullOrEmpty(_objs[objId].GainScript))
                        {
                            ExecuteScript(_objs[objId].GainScript, ctx);
                        }
                    }
                    else
                    {
                        MoveThing(_objs[objId].ObjectName, _currentRoom, Thing.Object, ctx);

                        if (!string.IsNullOrEmpty(_objs[objId].LoseScript))
                        {
                            ExecuteScript(_objs[objId].LoseScript, ctx);
                        }

                    }

                    foundObjectName = true;
                }

                if (!foundObjectName)
                {
                    LogASLError("No such object '" + item + "'", LogType.WarningError);
                }
                else
                {
                    UpdateItems(ctx);
                    UpdateObjectList(ctx);
                }
            }
            else
            {
                for (int i = 1, loopTo1 = _numberItems; i <= loopTo1; i++)
                {
                    if ((_items[i].Name ?? "") == (item ?? ""))
                    {
                        _items[i].Got = got;
                        i = _numberItems;
                    }
                }

                UpdateItems(ctx);
            }
        }

        internal void PlayGame(string room, Context ctx)
        {
            // plays the specified room

            int id = GetRoomID(room, ctx);

            if (id == 0)
            {
                LogASLError("No such room '" + room + "'", LogType.WarningError);
                return;
            }

            _currentRoom = room;

            SetStringContents("quest.currentroom", room, ctx);

            if (_gameAslVersion >= 391 & _gameAslVersion < 410)
            {
                AddToObjectProperties("visited", _rooms[id].ObjId, ctx);
            }

            ShowRoomInfo(room, ctx);
            UpdateItems(ctx);

            // Find script lines and execute them.

            if (!string.IsNullOrEmpty(_rooms[id].Script))
            {
                string script = _rooms[id].Script;
                ExecuteScript(script, ctx);
            }

            if (_gameAslVersion >= 410)
            {
                AddToObjectProperties("visited", _rooms[id].ObjId, ctx);
            }
        }

        internal void Print(string txt, Context ctx)
        {
            string printString = "";

            if (string.IsNullOrEmpty(txt))
            {
                DoPrint(printString);
            }
            else
            {
                for (int i = 1, loopTo = Strings.Len(txt); i <= loopTo; i++)
                {

                    bool printThis = true;

                    if (Strings.Mid(txt, i, 2) == "|w")
                    {
                        DoPrint(printString);
                        printString = "";
                        printThis = false;
                        i = i + 1;
                        ExecuteScript("wait <>", ctx);
                    }

                    else if (Strings.Mid(txt, i, 2) == "|c")
                    {
                        switch (Strings.Mid(txt, i, 3) ?? "")
                        {
                            // Do nothing - we don't want to remove the colour formatting codes.
                            case "|cb":
                            case "|cr":
                            case "|cl":
                            case "|cy":
                            case "|cg":
                                {
                                    break;
                                }

                            default:
                                {
                                    DoPrint(printString);
                                    printString = "";
                                    printThis = false;
                                    i = i + 1;
                                    ExecuteScript("clear", ctx);
                                    break;
                                }
                        }
                    }

                    if (printThis)
                        printString = printString + Strings.Mid(txt, i, 1);
                }

                if (!string.IsNullOrEmpty(printString))
                    DoPrint(printString);
            }
        }

        private string RetrLine(string blockType, string @param, string line, Context ctx)
        {
            DefineBlock searchblock;

            if (blockType == "object")
            {
                searchblock = GetThingBlock(@param, _currentRoom, Thing.Object);
            }
            else
            {
                searchblock = GetThingBlock(@param, _currentRoom, Thing.Character);
            }

            if (searchblock.StartLine == 0 & searchblock.EndLine == 0)
            {
                return "<undefined>";
            }

            for (int i = searchblock.StartLine + 1, loopTo = searchblock.EndLine - 1; i <= loopTo; i++)
            {
                if (BeginsWith(_lines[i], line))
                {
                    return Strings.Trim(_lines[i]);
                }
            }

            return "<unfound>";
        }

        private string RetrLineParam(string blockType, string @param, string line, string lineParam, Context ctx)
        {
            DefineBlock searchblock;

            if (blockType == "object")
            {
                searchblock = GetThingBlock(@param, _currentRoom, Thing.Object);
            }
            else
            {
                searchblock = GetThingBlock(@param, _currentRoom, Thing.Character);
            }

            if (searchblock.StartLine == 0 & searchblock.EndLine == 0)
            {
                return "<undefined>";
            }

            for (int i = searchblock.StartLine + 1, loopTo = searchblock.EndLine - 1; i <= loopTo; i++)
            {
                if (BeginsWith(_lines[i], line) && (Strings.LCase(GetParameter(_lines[i], ctx)) ?? "") == (Strings.LCase(lineParam) ?? ""))
                {
                    return Strings.Trim(_lines[i]);
                }
            }

            return "<unfound>";
        }

        private void SetUpCollectables()
        {
            bool lastItem = false;

            _numCollectables = 0;

            // Initialise collectables:
            // First, find the collectables section within the define
            // game block, and get its parameters:

            for (int a = GetDefineBlock("game").StartLine + 1, loopTo = GetDefineBlock("game").EndLine - 1; a <= loopTo; a++)
            {
                if (BeginsWith(_lines[a], "collectables "))
                {
                    string collectables = Strings.Trim(GetParameter(_lines[a], _nullContext, false));

                    // if collectables is a null string, there are no
                    // collectables. Otherwise, there is one more object than
                    // the number of commas. So, first check to see if we have
                    // no objects:

                    if (!string.IsNullOrEmpty(collectables))
                    {
                        _numCollectables = 1;
                        int pos = 1;
                        do
                        {
                            Array.Resize(ref _collectables, _numCollectables + 1);
                            _collectables[_numCollectables] = new Collectable();
                            int nextComma = Strings.InStr(pos + 1, collectables, ",");
                            if (nextComma == 0)
                            {
                                nextComma = Strings.InStr(pos + 1, collectables, ";");
                            }

                            // If there are no more commas, we want everything
                            // up to the end of the string, and then to exit
                            // the loop:
                            if (nextComma == 0)
                            {
                                nextComma = Strings.Len(collectables) + 1;
                                lastItem = true;
                            }

                            // Get item info
                            string info = Strings.Trim(Strings.Mid(collectables, pos, nextComma - pos));
                            _collectables[_numCollectables].Name = Strings.Trim(Strings.Left(info, Strings.InStr(info, " ")));

                            int ep = Strings.InStr(info, "=");
                            int sp1 = Strings.InStr(info, " ");
                            int sp2 = Strings.InStr(ep, info, " ");
                            if (sp2 == 0)
                                sp2 = Strings.Len(info) + 1;
                            string t = Strings.Trim(Strings.Mid(info, sp1 + 1, ep - sp1 - 1));
                            string i = Strings.Trim(Strings.Mid(info, ep + 1, sp2 - ep - 1));

                            if (Strings.Left(t, 1) == "d")
                            {
                                t = Strings.Mid(t, 2);
                                _collectables[_numCollectables].DisplayWhenZero = false;
                            }
                            else
                            {
                                _collectables[_numCollectables].DisplayWhenZero = true;
                            }

                            _collectables[_numCollectables].Type = t;
                            _collectables[_numCollectables].Value = Conversion.Val(i);

                            // Get display string between square brackets
                            int obp = Strings.InStr(info, "[");
                            int cbp = Strings.InStr(info, "]");
                            if (obp == 0)
                            {
                                _collectables[_numCollectables].Display = "<def>";
                            }
                            else
                            {
                                string b = Strings.Mid(info, obp + 1, cbp - 1 - obp);
                                _collectables[_numCollectables].Display = Strings.Trim(b);
                            }

                            pos = nextComma + 1;
                            _numCollectables = _numCollectables + 1;
                        }

                        // lastitem set when nextcomma=0, above.
                        while (lastItem != true);
                        _numCollectables = _numCollectables - 1;
                    }
                }
            }
        }

        private void SetUpItemArrays()
        {
            bool lastItem = false;

            _numberItems = 0;

            // Initialise items:
            // First, find the possitems section within the define game
            // block, and get its parameters:
            for (int a = GetDefineBlock("game").StartLine + 1, loopTo = GetDefineBlock("game").EndLine - 1; a <= loopTo; a++)
            {
                if (BeginsWith(_lines[a], "possitems ") | BeginsWith(_lines[a], "items "))
                {
                    string possItems = GetParameter(_lines[a], _nullContext);

                    if (!string.IsNullOrEmpty(possItems))
                    {
                        _numberItems = _numberItems + 1;
                        int pos = 1;
                        do
                        {
                            Array.Resize(ref _items, _numberItems + 1);
                            _items[_numberItems] = new ItemType();
                            int nextComma = Strings.InStr(pos + 1, possItems, ",");
                            if (nextComma == 0)
                            {
                                nextComma = Strings.InStr(pos + 1, possItems, ";");
                            }

                            // If there are no more commas, we want everything
                            // up to the end of the string, and then to exit
                            // the loop:
                            if (nextComma == 0)
                            {
                                nextComma = Strings.Len(possItems) + 1;
                                lastItem = true;
                            }

                            // Get item name
                            _items[_numberItems].Name = Strings.Trim(Strings.Mid(possItems, pos, nextComma - pos));
                            _items[_numberItems].Got = false;

                            pos = nextComma + 1;
                            _numberItems = _numberItems + 1;
                        }

                        // lastitem set when nextcomma=0, above.
                        while (lastItem != true);
                        _numberItems = _numberItems - 1;
                    }
                }
            }
        }

        private void SetUpStartItems()
        {
            bool lastItem = false;

            for (int a = GetDefineBlock("game").StartLine + 1, loopTo = GetDefineBlock("game").EndLine - 1; a <= loopTo; a++)
            {
                if (BeginsWith(_lines[a], "startitems "))
                {
                    string startItems = GetParameter(_lines[a], _nullContext);

                    if (!string.IsNullOrEmpty(startItems))
                    {
                        int pos = 1;
                        do
                        {
                            int nextComma = Strings.InStr(pos + 1, startItems, ",");
                            if (nextComma == 0)
                            {
                                nextComma = Strings.InStr(pos + 1, startItems, ";");
                            }

                            // If there are no more commas, we want everything
                            // up to the end of the string, and then to exit
                            // the loop:
                            if (nextComma == 0)
                            {
                                nextComma = Strings.Len(startItems) + 1;
                                lastItem = true;
                            }

                            // Get item name
                            string name = Strings.Trim(Strings.Mid(startItems, pos, nextComma - pos));

                            // Find which item this is, and set it
                            for (int i = 1, loopTo1 = _numberItems; i <= loopTo1; i++)
                            {
                                if ((_items[i].Name ?? "") == (name ?? ""))
                                {
                                    _items[i].Got = true;
                                    break;
                                }
                            }

                            pos = nextComma + 1;
                        }

                        // lastitem set when nextcomma=0, above.
                        while (lastItem != true);
                    }
                }
            }
        }

        private void ShowHelp(Context ctx)
        {
            // In Quest 4 and below, the help text displays in a separate window. In Quest 5, it displays
            // in the same window as the game text.
            Print("|b|cl|s14Quest Quick Help|xb|cb|s00", ctx);
            Print("", ctx);
            Print("|cl|bMoving|xb|cb Press the direction buttons in the 'Compass' pane, or type |bGO NORTH|xb, |bSOUTH|xb, |bE|xb, etc. |xn", ctx);
            Print("To go into a place, type |bGO TO ...|xb . To leave a place, type |bOUT, EXIT|xb or |bLEAVE|xb, or press the '|crOUT|cb' button.|n", ctx);
            Print("|cl|bObjects and Characters|xb|cb Use |bTAKE ...|xb, |bGIVE ... TO ...|xb, |bTALK|xb/|bSPEAK TO ...|xb, |bUSE ... ON|xb/|bWITH ...|xb, |bLOOK AT ...|xb, etc.|n", ctx);
            Print("|cl|bExit Quest|xb|cb Type |bQUIT|xb to leave Quest.|n", ctx);
            Print("|cl|bMisc|xb|cb Type |bABOUT|xb to get information on the current game. The next turn after referring to an object or character, you can use |bIT|xb, |bHIM|xb etc. as appropriate to refer to it/him/etc. again. If you make a mistake when typing an object's name, type |bOOPS|xb followed by your correction.|n", ctx);
            Print("|cl|bKeyboard shortcuts|xb|cb Press the |crup arrow|cb and |crdown arrow|cb to scroll through commands you have already typed in. Press |crEsc|cb to clear the command box.|n|n", ctx);
            Print("Further information is available by selecting |iQuest Documentation|xi from the |iHelp|xi menu.", ctx);
        }

        private void ReadCatalog(string data)
        {
            int nullPos = Strings.InStr(data, "\0");
            _numResources = Conversions.ToInteger(DecryptString(Strings.Left(data, nullPos - 1)));
            Array.Resize(ref _resources, _numResources + 1);

            data = Strings.Mid(data, nullPos + 1);

            int resourceStart = 0;

            for (int i = 1, loopTo = _numResources; i <= loopTo; i++)
            {
                _resources[i] = new ResourceType();
                var r = _resources[i];
                nullPos = Strings.InStr(data, "\0");
                r.ResourceName = DecryptString(Strings.Left(data, nullPos - 1));
                data = Strings.Mid(data, nullPos + 1);

                nullPos = Strings.InStr(data, "\0");
                r.ResourceLength = Conversions.ToInteger(DecryptString(Strings.Left(data, nullPos - 1)));
                data = Strings.Mid(data, nullPos + 1);

                r.ResourceStart = resourceStart;
                resourceStart = resourceStart + r.ResourceLength;

                r.Extracted = false;
            }
        }

        private void UpdateDirButtons(string dirs, Context ctx)
        {
            var compassExits = new List<ListData>();

            if (Strings.InStr(dirs, "n") > 0)
            {
                AddCompassExit(compassExits, "north");
            }

            if (Strings.InStr(dirs, "s") > 0)
            {
                AddCompassExit(compassExits, "south");
            }

            if (Strings.InStr(dirs, "w") > 0)
            {
                AddCompassExit(compassExits, "west");
            }

            if (Strings.InStr(dirs, "e") > 0)
            {
                AddCompassExit(compassExits, "east");
            }

            if (Strings.InStr(dirs, "o") > 0)
            {
                AddCompassExit(compassExits, "out");
            }

            if (Strings.InStr(dirs, "a") > 0)
            {
                AddCompassExit(compassExits, "northeast");
            }

            if (Strings.InStr(dirs, "b") > 0)
            {
                AddCompassExit(compassExits, "northwest");
            }

            if (Strings.InStr(dirs, "c") > 0)
            {
                AddCompassExit(compassExits, "southeast");
            }

            if (Strings.InStr(dirs, "d") > 0)
            {
                AddCompassExit(compassExits, "southwest");
            }

            if (Strings.InStr(dirs, "u") > 0)
            {
                AddCompassExit(compassExits, "up");
            }

            if (Strings.InStr(dirs, "f") > 0)
            {
                AddCompassExit(compassExits, "down");
            }

            _compassExits = compassExits;
            UpdateExitsList();
        }

        private void AddCompassExit(List<ListData> exitList, string name)
        {
            exitList.Add(new ListData(name, _listVerbs[ListType.ExitsList]));
        }

        private string UpdateDoorways(int roomId, Context ctx)
        {
            string roomDisplayText = "";
            string outPlace = "";
            string directions = "";
            string nsew = "";
            string outPlaceName = "";
            string outPlacePrefix = "";

            string n = "north";
            string s = "south";
            string e = "east";
            string w = "west";
            string ne = "northeast";
            string nw = "northwest";
            string se = "southeast";
            string sw = "southwest";
            string u = "up";
            string d = "down";

            if (_gameAslVersion >= 410)
            {
                _rooms[roomId].Exits.GetAvailableDirectionsDescription(ref roomDisplayText, ref directions);
            }
            else
            {

                if (!string.IsNullOrEmpty(_rooms[roomId].Out.Text))
                {
                    outPlace = _rooms[roomId].Out.Text;

                    // remove any prefix semicolon from printed text
                    int scp = Strings.InStr(outPlace, ";");
                    if (scp == 0)
                    {
                        outPlaceName = outPlace;
                    }
                    else
                    {
                        outPlaceName = Strings.Trim(Strings.Mid(outPlace, scp + 1));
                        outPlacePrefix = Strings.Trim(Strings.Left(outPlace, scp - 1));
                        outPlace = outPlacePrefix + " " + outPlaceName;
                    }
                }

                if (!string.IsNullOrEmpty(_rooms[roomId].North.Data))
                {
                    nsew = nsew + "|b" + n + "|xb, ";
                    directions = directions + "n";
                }
                if (!string.IsNullOrEmpty(_rooms[roomId].South.Data))
                {
                    nsew = nsew + "|b" + s + "|xb, ";
                    directions = directions + "s";
                }
                if (!string.IsNullOrEmpty(_rooms[roomId].East.Data))
                {
                    nsew = nsew + "|b" + e + "|xb, ";
                    directions = directions + "e";
                }
                if (!string.IsNullOrEmpty(_rooms[roomId].West.Data))
                {
                    nsew = nsew + "|b" + w + "|xb, ";
                    directions = directions + "w";
                }
                if (!string.IsNullOrEmpty(_rooms[roomId].NorthEast.Data))
                {
                    nsew = nsew + "|b" + ne + "|xb, ";
                    directions = directions + "a";
                }
                if (!string.IsNullOrEmpty(_rooms[roomId].NorthWest.Data))
                {
                    nsew = nsew + "|b" + nw + "|xb, ";
                    directions = directions + "b";
                }
                if (!string.IsNullOrEmpty(_rooms[roomId].SouthEast.Data))
                {
                    nsew = nsew + "|b" + se + "|xb, ";
                    directions = directions + "c";
                }
                if (!string.IsNullOrEmpty(_rooms[roomId].SouthWest.Data))
                {
                    nsew = nsew + "|b" + sw + "|xb, ";
                    directions = directions + "d";
                }
                if (!string.IsNullOrEmpty(_rooms[roomId].Up.Data))
                {
                    nsew = nsew + "|b" + u + "|xb, ";
                    directions = directions + "u";
                }
                if (!string.IsNullOrEmpty(_rooms[roomId].Down.Data))
                {
                    nsew = nsew + "|b" + d + "|xb, ";
                    directions = directions + "f";
                }

                if (!string.IsNullOrEmpty(outPlace))
                {
                    // see if outside has an alias

                    string outPlaceAlias = _rooms[GetRoomID(outPlaceName, ctx)].RoomAlias;
                    if (string.IsNullOrEmpty(outPlaceAlias))
                    {
                        outPlaceAlias = outPlace;
                    }
                    else if (_gameAslVersion >= 360)
                    {
                        if (!string.IsNullOrEmpty(outPlacePrefix))
                        {
                            outPlaceAlias = outPlacePrefix + " " + outPlaceAlias;
                        }
                    }

                    roomDisplayText = roomDisplayText + "You can go |bout|xb to " + outPlaceAlias + ".";
                    if (!string.IsNullOrEmpty(nsew))
                        roomDisplayText = roomDisplayText + " ";

                    directions = directions + "o";
                    if (_gameAslVersion >= 280)
                    {
                        SetStringContents("quest.doorways.out", outPlaceName, ctx);
                    }
                    else
                    {
                        SetStringContents("quest.doorways.out", outPlaceAlias, ctx);
                    }
                    SetStringContents("quest.doorways.out.display", outPlaceAlias, ctx);
                }
                else
                {
                    SetStringContents("quest.doorways.out", "", ctx);
                    SetStringContents("quest.doorways.out.display", "", ctx);
                }

                if (!string.IsNullOrEmpty(nsew))
                {
                    // strip final comma
                    nsew = Strings.Left(nsew, Strings.Len(nsew) - 2);
                    int cp = Strings.InStr(nsew, ",");
                    if (cp != 0)
                    {
                        bool finished = false;
                        do
                        {
                            int ncp = Strings.InStr(cp + 1, nsew, ",");
                            if (ncp == 0)
                            {
                                finished = true;
                            }
                            else
                            {
                                cp = ncp;
                            }
                        }
                        while (!finished);

                        nsew = Strings.Trim(Strings.Left(nsew, cp - 1)) + " or " + Strings.Trim(Strings.Mid(nsew, cp + 1));
                    }

                    roomDisplayText = roomDisplayText + "You can go " + nsew + ".";
                    SetStringContents("quest.doorways.dirs", nsew, ctx);
                }
                else
                {
                    SetStringContents("quest.doorways.dirs", "", ctx);
                }
            }

            UpdateDirButtons(directions, ctx);

            return roomDisplayText;
        }

        private void UpdateItems(Context ctx)
        {
            // displays the items a player has
            var invList = new List<ListData>();

            if (!_outPutOn)
                return;

            string name;

            if (_gameAslVersion >= 280)
            {
                for (int i = 1, loopTo = _numberObjs; i <= loopTo; i++)
                {
                    if (_objs[i].ContainerRoom == "inventory" & _objs[i].Exists & _objs[i].Visible)
                    {
                        if (string.IsNullOrEmpty(_objs[i].ObjectAlias))
                        {
                            name = _objs[i].ObjectName;
                        }
                        else
                        {
                            name = _objs[i].ObjectAlias;
                        }

                        invList.Add(new ListData(CapFirst(name), _listVerbs[ListType.InventoryList]));

                    }
                }
            }
            else
            {
                for (int j = 1, loopTo1 = _numberItems; j <= loopTo1; j++)
                {
                    if (_items[j].Got == true)
                    {
                        invList.Add(new ListData(CapFirst(_items[j].Name), _listVerbs[ListType.InventoryList]));
                    }
                }
            }

            UpdateList?.Invoke(ListType.InventoryList, invList);

            if (_gameAslVersion >= 284)
            {
                UpdateStatusVars(ctx);
            }
            else if (_numCollectables > 0)
            {

                string status = "";

                for (int j = 1, loopTo2 = _numCollectables; j <= loopTo2; j++)
                {
                    string k = DisplayCollectableInfo(j);
                    if (k != "<null>")
                    {
                        if (status.Length > 0)
                            status += Environment.NewLine;
                        status += k;
                    }
                }

                _player.SetStatusText(status);

            }
        }

        private void FinishGame(StopType stopType, Context ctx)
        {
            if (stopType == StopType.Win)
            {
                DisplayTextSection("win", ctx);
            }
            else if (stopType == StopType.Lose)
            {
                DisplayTextSection("lose", ctx);
            }

            GameFinished();
        }

        private void UpdateObjectList(Context ctx)
        {
            // Updates object list
            string shownPlaceName;
            string objSuffix;
            string charsViewable = "";
            var charsFound = default(int);
            string noFormatObjsViewable, charList;
            string objsViewable = "";
            var objsFound = default(int);
            string objListString, noFormatObjListString;

            if (!_outPutOn)
                return;

            var objList = new List<ListData>();
            var exitList = new List<ListData>();

            // find the room
            DefineBlock roomBlock;
            roomBlock = DefineBlockParam("room", _currentRoom);

            // FIND CHARACTERS ===
            if (_gameAslVersion < 281)
            {
                // go through Chars() array
                for (int i = 1, loopTo = _numberChars; i <= loopTo; i++)
                {
                    if ((_chars[i].ContainerRoom ?? "") == (_currentRoom ?? "") & _chars[i].Exists & _chars[i].Visible)
                    {
                        AddToObjectList(objList, exitList, _chars[i].ObjectName, Thing.Character);
                        charsViewable = charsViewable + _chars[i].Prefix + "|b" + _chars[i].ObjectName + "|xb" + _chars[i].Suffix + ", ";
                        charsFound = charsFound + 1;
                    }
                }

                if (charsFound == 0)
                {
                    SetStringContents("quest.characters", "", ctx);
                }
                else
                {
                    // chop off final comma and add full stop (.)
                    charList = Strings.Left(charsViewable, Strings.Len(charsViewable) - 2);
                    SetStringContents("quest.characters", charList, ctx);
                }
            }

            // FIND OBJECTS
            noFormatObjsViewable = "";

            for (int i = 1, loopTo1 = _numberObjs; i <= loopTo1; i++)
            {
                if ((Strings.LCase(_objs[i].ContainerRoom) ?? "") == (Strings.LCase(_currentRoom) ?? "") & _objs[i].Exists & _objs[i].Visible & !_objs[i].IsExit)
                {
                    objSuffix = _objs[i].Suffix;
                    if (!string.IsNullOrEmpty(objSuffix))
                        objSuffix = " " + objSuffix;
                    if (string.IsNullOrEmpty(_objs[i].ObjectAlias))
                    {
                        AddToObjectList(objList, exitList, _objs[i].ObjectName, Thing.Object);
                        objsViewable = objsViewable + _objs[i].Prefix + "|b" + _objs[i].ObjectName + "|xb" + objSuffix + ", ";
                        noFormatObjsViewable = noFormatObjsViewable + _objs[i].Prefix + _objs[i].ObjectName + ", ";
                    }
                    else
                    {
                        AddToObjectList(objList, exitList, _objs[i].ObjectAlias, Thing.Object);
                        objsViewable = objsViewable + _objs[i].Prefix + "|b" + _objs[i].ObjectAlias + "|xb" + objSuffix + ", ";
                        noFormatObjsViewable = noFormatObjsViewable + _objs[i].Prefix + _objs[i].ObjectAlias + ", ";
                    }
                    objsFound = objsFound + 1;
                }
            }

            if (objsFound != 0)
            {
                objListString = Strings.Left(objsViewable, Strings.Len(objsViewable) - 2);
                noFormatObjListString = Strings.Left(noFormatObjsViewable, Strings.Len(noFormatObjsViewable) - 2);
                SetStringContents("quest.objects", Strings.Left(noFormatObjsViewable, Strings.Len(noFormatObjsViewable) - 2), ctx);
                SetStringContents("quest.formatobjects", objListString, ctx);
            }
            else
            {
                SetStringContents("quest.objects", "", ctx);
                SetStringContents("quest.formatobjects", "", ctx);
            }

            // FIND DOORWAYS
            int roomId;
            roomId = GetRoomID(_currentRoom, ctx);
            if (roomId > 0)
            {
                if (_gameAslVersion >= 410)
                {
                    foreach (LegacyASL.RoomExit roomExit in _rooms[roomId].Exits.GetPlaces().Values)
                        this.AddToObjectList(objList, exitList, roomExit.GetDisplayName(), Thing.Room);
                }
                else
                {
                    var r = _rooms[roomId];

                    for (int i = 1, loopTo2 = r.NumberPlaces; i <= loopTo2; i++)
                    {

                        if (_gameAslVersion >= 311 & string.IsNullOrEmpty(_rooms[roomId].Places[i].Script))
                        {
                            int PlaceID = GetRoomID(_rooms[roomId].Places[i].PlaceName, ctx);
                            if (PlaceID == 0)
                            {
                                shownPlaceName = _rooms[roomId].Places[i].PlaceName;
                            }
                            else if (!string.IsNullOrEmpty(_rooms[PlaceID].RoomAlias))
                            {
                                shownPlaceName = _rooms[PlaceID].RoomAlias;
                            }
                            else
                            {
                                shownPlaceName = _rooms[roomId].Places[i].PlaceName;
                            }
                        }
                        else
                        {
                            shownPlaceName = _rooms[roomId].Places[i].PlaceName;
                        }

                        AddToObjectList(objList, exitList, shownPlaceName, Thing.Room);
                    }
                }
            }

            UpdateList?.Invoke(ListType.ObjectsList, objList);
            _gotoExits = exitList;
            UpdateExitsList();
        }

        private void UpdateExitsList()
        {
            // The Quest 5.0 Player takes a combined list of compass and "go to" exits, whereas the
            // ASL4 code produces these separately. So we keep track of them separately and then
            // merge to send to the Player.

            var mergedList = new List<ListData>();

            foreach (ListData listItem in _compassExits)
                mergedList.Add(listItem);

            foreach (ListData listItem in _gotoExits)
                mergedList.Add(listItem);

            UpdateList?.Invoke(ListType.ExitsList, mergedList);
        }

        private void UpdateStatusVars(Context ctx)
        {
            string displayData;
            string status = "";

            if (_numDisplayStrings > 0)
            {
                for (int i = 1, loopTo = _numDisplayStrings; i <= loopTo; i++)
                {
                    displayData = DisplayStatusVariableInfo(i, VarType.String, ctx);

                    if (!string.IsNullOrEmpty(displayData))
                    {
                        if (status.Length > 0)
                            status += Environment.NewLine;
                        status += displayData;
                    }
                }
            }

            if (_numDisplayNumerics > 0)
            {
                for (int i = 1, loopTo1 = _numDisplayNumerics; i <= loopTo1; i++)
                {
                    displayData = DisplayStatusVariableInfo(i, VarType.Numeric, ctx);
                    if (!string.IsNullOrEmpty(displayData))
                    {
                        if (status.Length > 0)
                            status += Environment.NewLine;
                        status += displayData;
                    }
                }
            }

            _player.SetStatusText(status);
        }

        private void UpdateVisibilityInContainers(Context ctx, string onlyParent = "")
        {
            // Use OnlyParent to only update objects that are contained by a specific parent

            int parentId;
            string parent;
            bool parentIsTransparent = default, parentIsOpen = default, parentIsSeen = default;
            var parentIsSurface = default(bool);

            if (_gameAslVersion < 391)
                return;

            if (!string.IsNullOrEmpty(onlyParent))
            {
                onlyParent = Strings.LCase(onlyParent);
                parentId = GetObjectIdNoAlias(onlyParent);

                parentIsOpen = IsYes(GetObjectProperty("opened", parentId, true, false));
                parentIsTransparent = IsYes(GetObjectProperty("transparent", parentId, true, false));
                parentIsSeen = IsYes(GetObjectProperty("seen", parentId, true, false));
                parentIsSurface = IsYes(GetObjectProperty("surface", parentId, true, false));
            }

            for (int i = 1, loopTo = _numberObjs; i <= loopTo; i++)
            {
                // If object has a parent object
                parent = GetObjectProperty("parent", i, false, false);

                if (!string.IsNullOrEmpty(parent))
                {

                    // Check if that parent is open, or transparent
                    if (string.IsNullOrEmpty(onlyParent))
                    {
                        parentId = GetObjectIdNoAlias(parent);
                        parentIsOpen = IsYes(GetObjectProperty("opened", parentId, true, false));
                        parentIsTransparent = IsYes(GetObjectProperty("transparent", parentId, true, false));
                        parentIsSeen = IsYes(GetObjectProperty("seen", parentId, true, false));
                        parentIsSurface = IsYes(GetObjectProperty("surface", parentId, true, false));
                    }

                    if (string.IsNullOrEmpty(onlyParent) | (Strings.LCase(parent) ?? "") == (onlyParent ?? ""))
                    {

                        if (parentIsSurface | (parentIsOpen | parentIsTransparent) & parentIsSeen)
                        {
                            // If the parent is a surface, then the contents are always available.
                            // Otherwise, only if the parent has been seen, AND is either open or transparent,
                            // then the contents are available.

                            SetAvailability(_objs[i].ObjectName, true, ctx);
                        }
                        else
                        {
                            SetAvailability(_objs[i].ObjectName, false, ctx);
                        }

                    }
                }
            }

        }

        private class PlayerCanAccessObjectResult
        {
            public bool CanAccessObject;
            public string ErrorMsg;
        }

        private PlayerCanAccessObjectResult PlayerCanAccessObject(int id, List<int> colObjects = null)
        {
            // Called to see if a player can interact with an object (take it, open it etc.).
            // For example, if the object is on a surface which is inside a closed container,
            // the object cannot be accessed.

            string parent;
            int parentId;
            string parentDisplayName;
            var result = new PlayerCanAccessObjectResult();

            string hierarchy = "";
            if (IsYes(GetObjectProperty("parent", id, true, false)))
            {

                // Object is in a container...

                parent = GetObjectProperty("parent", id, false, false);
                parentId = GetObjectIdNoAlias(parent);

                // But if it's a surface then it's OK

                if (!IsYes(GetObjectProperty("surface", parentId, true, false)) & !IsYes(GetObjectProperty("opened", parentId, true, false)))
                {
                    // Parent has no "opened" property, so it's closed. Hence
                    // object can't be accessed

                    if (!string.IsNullOrEmpty(_objs[parentId].ObjectAlias))
                    {
                        parentDisplayName = _objs[parentId].ObjectAlias;
                    }
                    else
                    {
                        parentDisplayName = _objs[parentId].ObjectName;
                    }

                    result.CanAccessObject = false;
                    result.ErrorMsg = "inside closed " + parentDisplayName;
                    return result;
                }

                // Is the parent itself accessible?
                if (colObjects is null)
                {
                    colObjects = new List<int>();
                }

                if (colObjects.Contains(parentId))
                {
                    // We've already encountered this parent while recursively calling
                    // this function - we're in a loop of parents!
                    foreach (int objId in colObjects)
                        hierarchy = hierarchy + _objs[objId].ObjectName + " -> ";
                    hierarchy = hierarchy + _objs[parentId].ObjectName;
                    LogASLError("Looped object parents detected: " + hierarchy);

                    result.CanAccessObject = false;
                    return result;
                }

                colObjects.Add(parentId);

                return PlayerCanAccessObject(parentId, colObjects);
            }

            result.CanAccessObject = true;
            return result;
        }

        private string GetGoToExits(int roomId, Context ctx)
        {
            string placeList = "";
            string shownPlaceName;

            for (int i = 1, loopTo = _rooms[roomId].NumberPlaces; i <= loopTo; i++)
            {
                if (_gameAslVersion >= 311 & string.IsNullOrEmpty(_rooms[roomId].Places[i].Script))
                {
                    int PlaceID = GetRoomID(_rooms[roomId].Places[i].PlaceName, ctx);
                    if (PlaceID == 0)
                    {
                        LogASLError("No such room '" + _rooms[roomId].Places[i].PlaceName + "'", LogType.WarningError);
                        shownPlaceName = _rooms[roomId].Places[i].PlaceName;
                    }
                    else if (!string.IsNullOrEmpty(_rooms[PlaceID].RoomAlias))
                    {
                        shownPlaceName = _rooms[PlaceID].RoomAlias;
                    }
                    else
                    {
                        shownPlaceName = _rooms[roomId].Places[i].PlaceName;
                    }
                }
                else
                {
                    shownPlaceName = _rooms[roomId].Places[i].PlaceName;
                }

                string shownPrefix = _rooms[roomId].Places[i].Prefix;
                if (!string.IsNullOrEmpty(shownPrefix))
                    shownPrefix = shownPrefix + " ";

                placeList = placeList + shownPrefix + "|b" + shownPlaceName + "|xb, ";
            }

            return placeList;
        }

        private void SetUpExits()
        {
            // Exits have to be set up after all the rooms have been initialised

            for (int i = 1, loopTo = _numberSections; i <= loopTo; i++)
            {
                if (BeginsWith(_lines[_defineBlocks[i].StartLine], "define room "))
                {
                    string roomName = GetParameter(_lines[_defineBlocks[i].StartLine], _nullContext);
                    int roomId = GetRoomID(roomName, _nullContext);

                    for (int j = _defineBlocks[i].StartLine + 1, loopTo1 = _defineBlocks[i].EndLine - 1; j <= loopTo1; j++)
                    {
                        if (BeginsWith(_lines[j], "define "))
                        {
                            // skip nested blocks
                            int nestedBlock = 1;
                            do
                            {
                                j = j + 1;
                                if (BeginsWith(_lines[j], "define "))
                                {
                                    nestedBlock = nestedBlock + 1;
                                }
                                else if (Strings.Trim(_lines[j]) == "end define")
                                {
                                    nestedBlock = nestedBlock - 1;
                                }
                            }
                            while (nestedBlock != 0);
                        }

                        _rooms[roomId].Exits.AddExitFromTag(_lines[j]);
                    }
                }
            }

            return;

        }

        private LegacyASL.RoomExit FindExit(string tag)
        {
            // e.g. Takes a tag of the form "room; north" and return's the north exit of room.

            string[] @params = Strings.Split(tag, ";");
            if (Information.UBound(@params) < 1)
            {
                LogASLError("No exit specified in '" + tag + "'", LogType.WarningError);
                return new LegacyASL.RoomExit(this);
            }

            string room = Strings.Trim(@params[0]);
            string exitName = Strings.Trim(@params[1]);

            int roomId = GetRoomID(room, _nullContext);

            if (roomId == 0)
            {
                LogASLError("Can't find room '" + room + "'", LogType.WarningError);
                return (LegacyASL.RoomExit)null;
            }

            var exits = _rooms[roomId].Exits;
            var dir = exits.GetDirectionEnum(ref exitName);
            if (dir == Direction.None)
            {
                if (exits.GetPlaces().ContainsKey(exitName))
                {
                    return exits.GetPlaces()[exitName];
                }
            }
            else
            {
                return exits.GetDirectionExit(ref dir);
            }

            return (LegacyASL.RoomExit)null;
        }

        private void ExecuteLock(string tag, bool @lock)
        {
            LegacyASL.RoomExit roomExit;

            roomExit = FindExit(tag);

            if (roomExit is null)
            {
                LogASLError("Can't find exit '" + tag + "'", LogType.WarningError);
                return;
            }

            roomExit.SetIsLocked(@lock);
        }

        public void Begin()
        {
            var runnerThread = new System.Threading.Thread(new System.Threading.ThreadStart(DoBegin));
            ChangeState(State.Working);
            runnerThread.Start();

            lock (_stateLock)
            {
                while (_state == State.Working & !_gameFinished)
                    System.Threading.Monitor.Wait(_stateLock);
            }
        }

        private void DoBegin()
        {
            var gameBlock = GetDefineBlock("game");
            var ctx = new Context();

            SetFont("");
            SetFontSize(0d);

            for (int i = GetDefineBlock("game").StartLine + 1, loopTo = GetDefineBlock("game").EndLine - 1; i <= loopTo; i++)
            {
                if (BeginsWith(_lines[i], "background "))
                {
                    SetBackground(GetParameter(_lines[i], _nullContext));
                }
            }

            for (int i = GetDefineBlock("game").StartLine + 1, loopTo1 = GetDefineBlock("game").EndLine - 1; i <= loopTo1; i++)
            {
                if (BeginsWith(_lines[i], "foreground "))
                {
                    SetForeground(GetParameter(_lines[i], _nullContext));
                }
            }

            // Execute any startscript command that appears in the
            // "define game" block:

            _autoIntro = true;

            // For ASL>=391, we only run startscripts if LoadMethod is normal (i.e. we haven't started
            // from a saved QSG file)

            if (_gameAslVersion < 391 | _gameAslVersion >= 391 & _gameLoadMethod == "normal")
            {

                // for GameASLVersion 311 and later, any library startscript is executed first:
                if (_gameAslVersion >= 311)
                {
                    // We go through the game block executing these in reverse order, as
                    // the statements which are included last should be executed first.
                    for (int i = gameBlock.EndLine - 1, loopTo2 = gameBlock.StartLine + 1; i >= loopTo2; i -= 1)
                    {
                        if (BeginsWith(_lines[i], "lib startscript "))
                        {
                            ctx = _nullContext;
                            ExecuteScript(Strings.Trim(GetEverythingAfter(Strings.Trim(_lines[i]), "lib startscript ")), ctx);
                        }
                    }
                }

                for (int i = gameBlock.StartLine + 1, loopTo3 = gameBlock.EndLine - 1; i <= loopTo3; i++)
                {
                    if (BeginsWith(_lines[i], "startscript "))
                    {
                        ctx = _nullContext;
                        ExecuteScript(Strings.Trim(GetEverythingAfter(Strings.Trim(_lines[i]), "startscript")), ctx);
                    }
                    else if (BeginsWith(_lines[i], "lib startscript ") & _gameAslVersion < 311)
                    {
                        ctx = _nullContext;
                        ExecuteScript(Strings.Trim(GetEverythingAfter(Strings.Trim(_lines[i]), "lib startscript ")), ctx);
                    }
                }

            }

            _gameFullyLoaded = true;

            // Display intro text
            if (_autoIntro & _gameLoadMethod == "normal")
                DisplayTextSection("intro", _nullContext);

            // Start game from room specified by "start" statement
            string startRoom = "";
            for (int i = gameBlock.StartLine + 1, loopTo4 = gameBlock.EndLine - 1; i <= loopTo4; i++)
            {
                if (BeginsWith(_lines[i], "start "))
                {
                    startRoom = GetParameter(_lines[i], _nullContext);
                }
            }

            if (!_loadedFromQsg)
            {
                ctx = _nullContext;
                PlayGame(startRoom, ctx);
                Print("", _nullContext);
            }
            else
            {
                UpdateItems(_nullContext);

                Print("Restored saved game", _nullContext);
                Print("", _nullContext);
                PlayGame(_currentRoom, _nullContext);
                Print("", _nullContext);

                if (_gameAslVersion >= 391)
                {
                    // For ASL>=391, OnLoad is now run for all games.
                    ctx = _nullContext;
                    ExecuteScript(_onLoadScript, ctx);
                }

            }

            RaiseNextTimerTickRequest();

            ChangeState(State.Ready);
        }

        public List<string> Errors
        {
            get
            {
                return new List<string>();
            }
        }

        public void Finish()
        {
            GameFinished();
        }

        public event FinishedHandler Finished;

        public event ErrorHandler LogError;

        public event PrintTextHandler PrintText;

        public void Save(string filename, string html)
        {
            SaveGame(filename);
        }

        public byte[] Save(string html)
        {
            return SaveGame(_gameData.Filename, false);
        }

        public void SendCommand(string command)
        {
            SendCommand(command, 0, null);
        }

        public void SendCommand(string command, IDictionary<string, string> metadata)
        {
            SendCommand(command, 0, metadata);
        }

        public void SendCommand(string command, int elapsedTime, IDictionary<string, string> metadata)
        {
            // The processing of commands is done in a separate thread, so things like the "enter" command can
            // lock the thread while waiting for further input. After starting to process the command, we wait
            // for something to happen before returning from the SendCommand call - either the command will have
            // finished processing, or perhaps a prompt has been printed and now the game is waiting for further
            // user input after hitting an "enter" script command.

            if (!_readyForCommand)
                return;

            var runnerThread = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(ProcessCommandInNewThread));
            ChangeState(State.Working);
            runnerThread.Start(command);

            WaitForStateChange(State.Working);

            if (elapsedTime > 0)
            {
                Tick(elapsedTime);
            }
            else
            {
                RaiseNextTimerTickRequest();
            }

        }

        private void WaitForStateChange(State changedFromState)
        {
            lock (_stateLock)
            {
                while (_state == changedFromState & !_gameFinished)
                    System.Threading.Monitor.Wait(_stateLock);
            }
        }

        private void ProcessCommandInNewThread(object command)
        {
            // Process command, and change state to Ready if the command finished processing

            try
            {
                if (ExecCommand((string)command, new Context()))
                {
                    ChangeState(State.Ready);
                }
            }
            catch (Exception ex)
            {
                LogException(ex);
                ChangeState(State.Ready);
            }
        }

        public void SendEvent(string eventName, string @param)
        {

        }

        public event UpdateListHandler UpdateList;

        public async Task<bool> Initialise(IPlayer player, bool? isCompiled = default)
        {
            _player = player;
            if (Strings.LCase(Strings.Right(_gameData.Filename, 4)) == ".qsg")
            {
                return OpenGame(_gameData.Filename);
            }
            else
            {
                return await InitialiseGame(_gameData);
            }
        }

        private void GameFinished()
        {
            _gameFinished = true;

            Finished?.Invoke();

            ChangeState(State.Finished);

            // In case we're in the middle of processing an "enter" command, nudge the thread along
            lock (_commandLock)
                System.Threading.Monitor.PulseAll(_commandLock);

            lock (_waitLock)
                System.Threading.Monitor.PulseAll(_waitLock);

            lock (_stateLock)
                System.Threading.Monitor.PulseAll(_stateLock);

            Cleanup();
        }

        private string GetResourcePath(string filename)
        {
            if (_resourceFile is not null && _resourceFile.Length > 0)
            {
                string extractResult = ExtractFile(filename);
                return extractResult;
            }
            return System.IO.Path.Combine(_gamePath, filename);
        }

        string IASL.GetResourcePath(string filename) => GetResourcePath(filename);

        private void Cleanup()
        {
            DeleteDirectory(_tempFolder);
        }

        private void DeleteDirectory(string dir)
        {
            if (System.IO.Directory.Exists(dir))
            {
                try
                {
                    System.IO.Directory.Delete(dir, true);
                }
                catch
                {
                }
            }
        }

        ~LegacyGame()
        {
            Cleanup();
        }

        private string[] GetLibraryLines(string libName)
        {
            byte[] libCode = null;
            libName = Strings.LCase(libName);

            switch (libName ?? "")
            {
                case "stdverbs.lib":
                    {
                        libCode = Resources.GetResourceBytes(Resources.stdverbs);
                        break;
                    }
                case "standard.lib":
                    {
                        libCode = Resources.GetResourceBytes(Resources.standard);
                        break;
                    }
                case "q3ext.qlb":
                    {
                        libCode = Resources.GetResourceBytes(Resources.q3ext);
                        break;
                    }
                case "typelib.qlb":
                    {
                        libCode = Resources.GetResourceBytes(Resources.Typelib);
                        break;
                    }
                case "net.lib":
                    {
                        libCode = Resources.GetResourceBytes(Resources.net);
                        break;
                    }
            }

            if (libCode is null)
                return null;

            return GetResourceLines(libCode);
        }

        public string SaveExtension
        {
            get
            {
                return "qsg";
            }
        }

        public void Tick(int elapsedTime)
        {
            int i;
            var timerScripts = new List<string>();

            Debug.Print("Tick: " + elapsedTime.ToString());

            var loopTo = _numberTimers;
            for (i = 1; i <= loopTo; i++)
            {
                if (_timers[i].TimerActive)
                {
                    if (_timers[i].BypassThisTurn)
                    {
                        // don't trigger timer during the turn it was first enabled
                        _timers[i].BypassThisTurn = false;
                    }
                    else
                    {
                        _timers[i].TimerTicks = _timers[i].TimerTicks + elapsedTime;

                        if (_timers[i].TimerTicks >= _timers[i].TimerInterval)
                        {
                            _timers[i].TimerTicks = 0;
                            timerScripts.Add(_timers[i].TimerAction);
                        }
                    }
                }
            }

            if (timerScripts.Count > 0)
            {
                var runnerThread = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(RunTimersInNewThread));

                ChangeState(State.Working);
                runnerThread.Start(timerScripts);
                WaitForStateChange(State.Working);
            }

            RaiseNextTimerTickRequest();
        }

        private void RunTimersInNewThread(object scripts)
        {
            List<string> scriptList = (List<string>)scripts;

            foreach (string script in scriptList)
            {
                try
                {
                    ExecuteScript(script, _nullContext);
                }
                catch (Exception ex)
                {
                    LogException(ex);
                }
            }

            ChangeState(State.Ready);
        }

        private void RaiseNextTimerTickRequest()
        {
            bool anyTimerActive = false;
            int nextTrigger = 60;

            for (int i = 1, loopTo = _numberTimers; i <= loopTo; i++)
            {
                if (_timers[i].TimerActive)
                {
                    anyTimerActive = true;

                    int thisNextTrigger = _timers[i].TimerInterval - _timers[i].TimerTicks;
                    if (thisNextTrigger < nextTrigger)
                    {
                        nextTrigger = thisNextTrigger;
                    }
                }
            }

            if (!anyTimerActive)
                nextTrigger = 0;
            if (_gameFinished)
                nextTrigger = 0;

            Debug.Print("RaiseNextTimerTickRequest " + nextTrigger.ToString());

            RequestNextTimerTick?.Invoke(nextTrigger);
        }

        private void ChangeState(State newState)
        {
            bool acceptCommands = newState == State.Ready;
            ChangeState(newState, acceptCommands);
        }

        private void ChangeState(State newState, bool acceptCommands)
        {
            _readyForCommand = acceptCommands;
            lock (_stateLock)
            {
                _state = newState;
                System.Threading.Monitor.PulseAll(_stateLock);
            }
        }

        public void FinishWait()
        {
            if (_state != State.Waiting)
                return;
            var runnerThread = new System.Threading.Thread(new System.Threading.ThreadStart(FinishWaitInNewThread));
            ChangeState(State.Working);
            runnerThread.Start();
            WaitForStateChange(State.Working);
        }

        private void FinishWaitInNewThread()
        {
            lock (_waitLock)
                System.Threading.Monitor.PulseAll(_waitLock);
        }

        public void FinishPause()
        {
            FinishWait();
        }

        private string m_menuResponse;

        private string ShowMenu(MenuData menuData)
        {
            _player.ShowMenu(menuData);
            ChangeState(State.Waiting);

            lock (_waitLock)
                System.Threading.Monitor.Wait(_waitLock);

            return m_menuResponse;
        }

        public void SetMenuResponse(string response)
        {
            var runnerThread = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(SetMenuResponseInNewThread));
            ChangeState(State.Working);
            runnerThread.Start(response);
            WaitForStateChange(State.Working);
        }

        private void SetMenuResponseInNewThread(object response)
        {
            m_menuResponse = (string)response;

            lock (_waitLock)
                System.Threading.Monitor.PulseAll(_waitLock);
        }

        private void LogException(Exception ex)
        {
            LogError?.Invoke(ex.Message + Environment.NewLine + ex.StackTrace);
        }

        public IEnumerable<string> GetExternalScripts()
        {
            return null;
        }

        public IEnumerable<string> GetExternalStylesheets()
        {
            return null;
        }

        public event Action<int> RequestNextTimerTick;

        private string GetOriginalFilenameForQSG()
        {
            if (_originalFilename is not null)
                return _originalFilename;
            return _gameFileName;
        }

        public delegate string UnzipFunctionDelegate(string filename, out string tempDir);
        private UnzipFunctionDelegate m_unzipFunction;

        public void SetUnzipFunction(UnzipFunctionDelegate unzipFunction)
        {
            m_unzipFunction = unzipFunction;
        }

        private string GetUnzippedFile(string filename)
        {
            string tempDir = null;
            string result = m_unzipFunction.Invoke(filename, out tempDir);
            _tempFolder = tempDir;
            return result;
        }

        public string TempFolder
        {
            get
            {
                return _tempFolder;
            }
            set
            {
                _tempFolder = value;
            }
        }

        public int ASLVersion
        {
            get
            {
                return _gameAslVersion;
            }
        }

        public System.IO.Stream GetResource(string @file)
        {
            if (@file == "_game.cas")
            {
                return new System.IO.MemoryStream(GetResourcelessCAS());
            }
            string path = GetResourcePath(@file);
            if (!System.IO.File.Exists(path))
                return null;
            return new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read);
        }

        public string GameID
        {
            get
            {
                if (string.IsNullOrEmpty(_gameFileName))
                    return null;
                return Utility.Utility.FileMD5Hash(_gameFileName);
            }
        }

        public IEnumerable<string> GetResources()
        {
            for (int i = 1, loopTo = _numResources; i <= loopTo; i++)
                yield return _resources[i].ResourceName;
            if (_numResources > 0)
            {
                yield return "_game.cas";
            }
        }

        private byte[] GetResourcelessCAS()
        {
            string fileData = System.IO.File.ReadAllText(_resourceFile, System.Text.Encoding.GetEncoding(1252));
            return System.Text.Encoding.GetEncoding(1252).GetBytes(Strings.Left(fileData, _startCatPos - 1));
        }

    }
}
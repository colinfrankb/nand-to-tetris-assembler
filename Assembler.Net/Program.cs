using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Assembler.Net
{
    class Program
    {
        static void Main(string[] args)
        {
            string[] assemblyInstructions = File.ReadAllLines(args[0]);
            
            var parser = new Parser();

            var binaryInstructions = parser.ParseAssemblyInstructions(assemblyInstructions);

            File.WriteAllLines(args[1], binaryInstructions);
        }
    }

    public class Parser
    {
        private Dictionary<string, string> _destDictionary;
        private Dictionary<string, string> _compDictionary;
        private Dictionary<string, string> _jumpDictionary;
        private Dictionary<string, int> _symbolDictionary;
        private int _nextVariableAddress = 0x0010;

        public IList<string> ParseAssemblyInstructions(string[] assemblyInstructions)
        {
            var remainingAssemblyInstructions = ParseLabelSymbols(assemblyInstructions);

            var binaryInstructions = new List<string>();

            // The second loop over the assemblyInstructions will only look for variable symbols
            foreach (var assemblyInstruction in remainingAssemblyInstructions)
            {
                var binaryInstruction = assemblyInstruction;

                if (IsAInstruction(assemblyInstruction))
                {
                    //A-Instruction binary format 0vvvvvvvvvvvvvvv
                    var addressField = assemblyInstruction.Split('@')[1];
                    var addressAsInteger = GetAddressAsInteger(addressField);
                    var addressAsBinaryString = GetAddressAsBinaryString(addressAsInteger);
                    binaryInstruction = addressAsBinaryString;
                }
                else
                {
                    binaryInstruction = "111";

                    //C-Instruction assembly format dest=comp;jump
                    //C-Instruction binary format 111accccccdddjjj
                    var (destField, compAndJumpFields) = GetDestField(assemblyInstruction.Split('='));
                    var (compField, jumpField) = GetCompAndJumpFields(compAndJumpFields.Split(';'));

                    // 111accccccdddjjj
                    binaryInstruction += compField.Contains('M') ? "1" : "0";

                    // comp will always have a value
                    binaryInstruction += CompDictionary[compField];

                    binaryInstruction += DestDictionary[destField];

                    binaryInstruction += JumpDictionary[jumpField];
                }


                binaryInstructions.Add(binaryInstruction);
            }

            return binaryInstructions;
        }

        private IList<string> ParseLabelSymbols(string[] assemblyInstructions)
        {
            var remainingAssemblyInstructions = new List<string>();

            for (int i = 0; i < assemblyInstructions.Length; i++)
            {
                var assemblyInstruction = GetNoncomment(assemblyInstructions[i]);

                if (string.IsNullOrWhiteSpace(assemblyInstruction))
                {
                    continue;
                }

                if (IsLabelSymbol(assemblyInstruction, out string labelSymbol))
                {
                    if (!SymbolDictionary.ContainsKey(labelSymbol))
                    {
                        SymbolDictionary[labelSymbol] = remainingAssemblyInstructions.Count; // The address should be the address of the proceeding instruction.
                    }

                    continue;
                }

                remainingAssemblyInstructions.Add(assemblyInstruction);
            }

            return remainingAssemblyInstructions;
        }

        private string GetNoncomment(string assemblyInstruction)
        {
            var indexOfComment = assemblyInstruction.IndexOf("//");

            if (indexOfComment > -1)
            {
                assemblyInstruction = assemblyInstruction.Substring(0, Math.Max(0, indexOfComment));
            }

            return assemblyInstruction.Trim();
        }

        private bool IsLabelSymbol(string assemblyInstruction, out string labelSymbol)
        {
            var labelSymbolRegex = new Regex(@"\((?<label>.*)\)");
            var match = labelSymbolRegex.Match(assemblyInstruction);

            labelSymbol = match.Value.Replace("(", "").Replace(")", "");

            return match.Success;
        }

        private Dictionary<string, string> DestDictionary
        {
            get
            {
                if (_destDictionary == null)
                {
                    _destDictionary = new Dictionary<string, string>
                    {
                        { "", "000" },
                        { "M", "001" },
                        { "D", "010" },
                        { "MD", "011" },
                        { "A", "100" },
                        { "AM", "101" },
                        { "AD", "110" },
                        { "AMD", "111" }
                    };
                }

                return _destDictionary;
            }
        }

        private Dictionary<string, string> CompDictionary
        {
            get
            {
                if (_compDictionary == null)
                {
                    // side note, based on a in the binary instruction, A or M will be passed into the ALU
                    _compDictionary = new Dictionary<string, string>
                    {
                        { "0", "101010" },
                        { "1", "111111" },
                        { "-1", "111010" },
                        { "D", "001100" },
                        { "A", "110000" },
                        { "M", "110000" },
                        { "!D", "001101" },
                        { "!A", "110001" },
                        { "!M", "110001" },
                        { "-D", "001111" },
                        { "-A", "110011" },
                        { "-M", "110011" },
                        { "D+1", "011111" },
                        { "A+1", "110111" },
                        { "M+1", "110111" },
                        { "D-1", "001110" },
                        { "A-1", "110010" },
                        { "M-1", "110010" },
                        { "D+A", "000010" },
                        { "D+M", "000010" },
                        { "D-A", "010011" },
                        { "D-M", "010011" },
                        { "A-D", "000111" },
                        { "M-D", "000111" },
                        { "D&A", "000000" },
                        { "D&M", "000000" },
                        { "D|A", "010101" },
                        { "D|M", "010101" }
                    };
                }

                return _compDictionary;
            }
        }

        private Dictionary<string, string> JumpDictionary
        {
            get
            {
                if (_jumpDictionary == null)
                {
                    _jumpDictionary = new Dictionary<string, string>
                    {
                        { "", "000" },
                        { "JGT", "001" },
                        { "JEQ", "010" },
                        { "JGE", "011" },
                        { "JLT", "100" },
                        { "JNE", "101" },
                        { "JLE", "110" },
                        { "JMP", "111" }
                    };
                }

                return _jumpDictionary;
            }
        }

        private Dictionary<string, int> SymbolDictionary
        {
            get
            {
                if(_symbolDictionary == null)
                {
                    _symbolDictionary = new Dictionary<string, int>
                    {
                        { "SP", 0x0 },
                        { "LCL", 0x1 },
                        { "ARG", 0x2 },
                        { "THIS", 0x3 },
                        { "THAT", 0x4 },
                        { "R0", 0x0 },
                        { "R1", 0x1 },
                        { "R2", 0x2 },
                        { "R3", 0x3 },
                        { "R4", 0x4 },
                        { "R5", 0x5 },
                        { "R6", 0x6 },
                        { "R7", 0x7 },
                        { "R8", 0x8 },
                        { "R9", 0x9 },
                        { "R10", 0xa },
                        { "R11", 0xb },
                        { "R12", 0xc },
                        { "R13", 0xd },
                        { "R14", 0xe },
                        { "R15", 0xf },
                        { "SCREEN", 0x4000 },
                        { "KBD", 0x6000 }
                    };
                }

                return _symbolDictionary;
            }
        }

        private (string, string) GetCompAndJumpFields(string[] compAndJumpFieldsSplit)
        {
            if (compAndJumpFieldsSplit.Length == 1)
                return (compAndJumpFieldsSplit[0], string.Empty);

            return (compAndJumpFieldsSplit[0], compAndJumpFieldsSplit[1]);
        }

        private (string DestField, string CompAndJumpFields) GetDestField(string[] assemblyInstructionSplit)
        {
            if (assemblyInstructionSplit.Length == 1)
                return (string.Empty, assemblyInstructionSplit[0]);

            return (assemblyInstructionSplit[0], assemblyInstructionSplit[1]);
        }

        private int GetAddressAsInteger(string addressField)
        {
            if (int.TryParse(addressField, out int addressFieldAsInteger))
            {
                return addressFieldAsInteger;
            }

            if (!SymbolDictionary.ContainsKey(addressField))
            {
                SymbolDictionary[addressField] = _nextVariableAddress;
                _nextVariableAddress++;
            }

            return SymbolDictionary[addressField];
        }

        private string GetAddressAsBinaryString(int addressAsInteger)
        {
            var binaryString = Convert.ToString(addressAsInteger, 2);

            return new string('0', 16 - binaryString.Length) + binaryString;
        }

        private bool IsAInstruction(string assemblyInstruction)
        {
            return assemblyInstruction.StartsWith("@");
        }
    }
}

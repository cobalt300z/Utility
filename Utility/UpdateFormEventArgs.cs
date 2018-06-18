using System;

namespace Utility
{
    public class UpdateFormEventArgs : EventArgs
    {
        private int[] _formInts;
        private bool[] _formBools;
        private string[] _formStrings;


        public UpdateFormEventArgs(int[] formints,  bool[] formbools, string[] formstrings)
        {
            _formInts = formints;
            _formBools = formbools;
            _formStrings = formstrings;
        }

        public int[] Ints
        {
            get
            {
                return _formInts;
            }
        }

        public bool[] Bools
        {
            get
            {
                return _formBools;
            }
        }
        
        public string[] Strings
        {
            get
            {
                return _formStrings;
            }
        }
    }
}

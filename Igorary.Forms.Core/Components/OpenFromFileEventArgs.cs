using System;
using System.Collections.Generic;
using System.Text;

namespace Igorary.Forms.Components
{
    public class OpenFromFileEventArgs: EventArgs
    {
        public string FilePath { get; private set; }

        public bool FileValid { get; set; }

        public OpenFromFileEventArgs(string filePath) {
            FilePath = filePath;
            FileValid = true;
        }
    }
}

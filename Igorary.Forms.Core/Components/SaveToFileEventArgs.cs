using System;
using System.Collections.Generic;
using System.Text;

namespace Igorary.Forms.Components
{
    public class SaveToFileEventArgs: EventArgs
    {
        public string FilePath { get; private set; }

        public SaveToFileEventArgs(string filePath) {
            FilePath = filePath;
        }
    }
}

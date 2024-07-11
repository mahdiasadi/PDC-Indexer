using System;
using System.Collections.Generic;
using System.Text;

namespace PdcMirrorIndexer
{
    class ClassGlobal
    {
        public static System.Guid gudid;

        public static string path;
 
   public static System.Guid Gudid
        {
            get { return gudid; }
            set { gudid = value; }
        }

   public static string  Path
   {
       get { return path; }
       set { path = value; }
   }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace FSELink.Entities
{
   public class SupperCodeEntity
   {
        public string barcode { get; set; }

        public string boxcode { get; set; }

        public string fwcode { get; set; }


        public string boxsecretcode { get; set; }

        public string pagecode { set; get; }


        public string codetype { get; set; }


        public string orderno { get; set; }
   }
}

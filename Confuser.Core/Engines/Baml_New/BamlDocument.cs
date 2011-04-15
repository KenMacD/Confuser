﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;

namespace Confuser.Core.Engines.Baml_New
{
    class BamlDocument : Collection<BamlRecord>
    {
        public struct BamlVersion
        {
            public ushort Major;
            public ushort Minor;
        }
        public string Signature { get; set; }
        public BamlVersion ReaderVersion { get; set; }
        public BamlVersion UpdaterVersion { get; set; }
        public BamlVersion WriterVersion { get; set; }
    }
}

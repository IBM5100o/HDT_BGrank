using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace HDT_BGrank
{
    internal class Mirror
    {
        private Process _process;

        private ScryDotNet.MonoScry _view;

        private ScryDotNet.MonoImage _root;

        public string ImageName { get; set; }

        public bool Active => _process != null;

        public Process Proc => _process ?? (_process = Process.GetProcessesByName(ImageName).FirstOrDefault());

        public ScryDotNet.MonoScry View
        {
            get
            {
                if (Proc == null)
                {
                    return null;
                }
                return _view ?? (_view = new ScryDotNet.MonoScry(ScryDotNet.Scry.connect(Proc.Id)));
            }
        }

        public ScryDotNet.MonoImage Root
        {
            get
            {
                if (_root != null)
                {
                    return _root;
                }
                _root = View?.getImage(new List<string> { "Blizzard.T5.ServiceLocator" }, "2021.3.25.61228");
                return _root;
            }
        }

        internal void Clean()
        {
            _process = null;
            _view = null;
            _root = null;
            GC.Collect();
        }
    }
}

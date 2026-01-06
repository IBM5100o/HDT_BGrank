using ScryDotNet;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

namespace HDT_BGrank
{
    public class Mirror
    {
        private Process _process;
        private MonoScry _view;
        private MonoImage _root;

        public string ImageName { get; set; }

        public bool Active => _process != null;

        public Process Proc => _process ?? (_process = Process.GetProcessesByName(ImageName).FirstOrDefault());

        public MonoScry View
        {
            get
            {
                if (Proc == null)
                {
                    return null;
                }
                return _view ?? (_view = new MonoScry(Scry.connect(Proc.Id)));
            }
        }

        public MonoImage Root
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

        public void Clean()
        {
            try
            {
                _process?.Dispose();
                _view?.Dispose();
                _root?.Dispose();
            }
            catch { /* ignore dispose errors */ }
            _process = null;
            _view = null;
            _root = null;
        }
    }
}
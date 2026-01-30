using ScryDotNet;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

namespace HDT_BGrank
{
    public class Mirror
    {
        private MonoImage _root;
        private string _myName;

        public MonoImage Root
        {
            get
            {
                if (_root != null) { return _root; }

                using Process proc = Process.GetProcessesByName("Hearthstone").FirstOrDefault();
                using MonoScry view = new MonoScry(Scry.connect(proc.Id));
                _root = view.getImage(new List<string> { "Blizzard.T5.ServiceLocator" }, "2021.3.25.61228");
                return _root;
            }
        }

        public string MyName
        {
            get
            {
                if (_myName != null) { return _myName; }

                dynamic presenceMgr = Root?["BnetPresenceMgr"]?["s_instance"];
                _myName = presenceMgr?["m_myPlayer"]?["m_account"]?["m_battleTag"]?["m_name"];
                return _myName;
            }
        }

        public void Clean()
        {
            _root?.Dispose();
            _root = null;
            _myName = null;
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace WindowExe.BossFight
{
    /// <summary>FastMouse merges state events before onEvent. Preserve those coordinates while a boss collector exists.</summary>
    internal sealed class BossInputCollectionLease : IDisposable
    {
        private sealed class Entry {internal int count;internal bool original;}
        private static readonly Dictionary<InputSettings,Entry> owners=new Dictionary<InputSettings,Entry>();
        private InputSettings settings;
        internal BossInputCollectionLease()
        {
            settings=InputSystem.settings;
            if(!owners.TryGetValue(settings,out var entry))
            {entry=new Entry{original=settings.disableRedundantEventsMerging};owners.Add(settings,entry);}
            entry.count++;settings.disableRedundantEventsMerging=true;
        }
        public void Dispose()
        {
            if(ReferenceEquals(settings,null))return;
            if(owners.TryGetValue(settings,out var entry) && --entry.count==0)
            {
                owners.Remove(settings);
                if(settings!=null && settings.disableRedundantEventsMerging)settings.disableRedundantEventsMerging=entry.original;
            }
            settings=null;
        }
    }
}

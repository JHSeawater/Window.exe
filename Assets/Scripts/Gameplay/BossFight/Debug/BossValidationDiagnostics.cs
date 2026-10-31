using System;
using System.IO;
using UnityEngine;

namespace WindowExe.BossFight
{
    /// <summary>Validation scenes only: local gameplay counters, never text entry or unrelated keys.</summary>
    [DefaultExecutionOrder(600)]
    public sealed class BossValidationDiagnostics : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public string utc,session,phase,outcome,pause;
            public int attempt,press,release,space,escape,samples,applied,late;
            public double download,elapsed,remaining,maxInputDelay;
            public bool minimized,invulnerable,downloadPaused,preserveRawMouse;
            public BossRect body;public BossPoint trash,boss;
        }
        private Report report=new Report();
        private BossFightController controller;
        private BossInputSource source;
        private double nextSave;
        private void OnEnable(){report=new Report();nextSave=0;}
        private void LateUpdate()
        {
            if(controller==null)controller=FindAnyObjectByType<BossFightController>();
            if(controller==null || !controller.IsActive)return;
            if(source!=controller.InputSource)
            {if(source!=null)source.Timeline.Intent-=OnIntent;source=controller.InputSource;if(source!=null)source.Timeline.Intent+=OnIntent;}
            if(Time.realtimeSinceStartupAsDouble>=nextSave){Save();nextSave=Time.realtimeSinceStartupAsDouble+2;}
        }
        private void OnIntent(BossInputIntent intent)
        {
            if(intent.Kind==BossInputKind.Press)report.press++;if(intent.Kind==BossInputKind.Release)report.release++;
            if(intent.Kind==BossInputKind.Space)report.space++;if(intent.Kind==BossInputKind.Escape)report.escape++;
        }
        private void Save()
        {
            if(controller==null || controller.Model==null || source==null)return;
            var model=controller.Model;var metrics=source.Timeline.Metrics;
            report.utc=DateTime.UtcNow.ToString("o");report.session=controller.Session?.SessionId??report.session;report.phase=model is BossPhase1Model?"Phase1":"Phase2";
            report.outcome=model.Outcome.ToString();report.pause=model.Clock.PauseReasons.ToString();report.attempt=model.Attempt;
            report.samples=metrics.samples;report.applied=metrics.applied;report.late=metrics.late;report.maxInputDelay=metrics.maxDelay;
            report.download=model.DownloadPercent;report.elapsed=model.Clock.DownloadElapsed;report.remaining=model.Clock.DownloadRemaining;
            report.minimized=model.Minimized;report.invulnerable=model.Invulnerable;report.downloadPaused=model.Clock.DownloadDebugPaused;
            report.preserveRawMouse=UnityEngine.InputSystem.InputSystem.settings.disableRedundantEventsMerging;
            report.body=model.Body;report.trash=model.Trash;report.boss=model.Boss;
            var folder=Application.isEditor?Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Builds/BossFight"):Directory.GetParent(Application.dataPath).FullName;
            try{Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"boss-session.json"),JsonUtility.ToJson(report,true));}
            catch(IOException e){Debug.LogWarning("Boss validation report: "+e.Message);}
        }
        private void OnDisable(){Save();if(source!=null)source.Timeline.Intent-=OnIntent;source=null;controller=null;}
    }
}

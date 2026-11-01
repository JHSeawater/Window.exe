using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Object=UnityEngine.Object;

namespace WindowExe.BossFight
{
    /// <summary>One session-owned ending movie, drawn over the existing black screen.</summary>
    internal sealed class BossEndingVideo : IDisposable
    {
        private readonly GameObject root;
        private readonly VideoPlayer player;
        private readonly AudioSource audio;
        private readonly RawImage image;
        private readonly RenderTexture texture;
        private bool paused,disposed;
        private double lastUpdate,preparationSeconds;
        internal bool Finished {get;private set;}
        internal bool Failed {get;private set;}
        internal bool Busy=>!Finished && !Failed && !disposed;

        internal BossEndingVideo(Transform parent,VideoClip clip)
        {
            root=new GameObject("Boss ending video",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter),typeof(VideoPlayer),typeof(AudioSource));
            root.transform.SetParent(parent,false);
            var rect=(RectTransform)root.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var aspect=root.GetComponent<AspectRatioFitter>();aspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;aspect.aspectRatio=(float)clip.width/clip.height;
            texture=new RenderTexture((int)clip.width,(int)clip.height,0,RenderTextureFormat.ARGB32){name="Boss ending video (Runtime)"};texture.Create();
            image=root.GetComponent<RawImage>();image.texture=texture;image.color=Color.white;image.enabled=false;
            audio=root.GetComponent<AudioSource>();audio.playOnAwake=false;audio.loop=false;audio.spatialBlend=0;audio.volume=.9f;
            player=root.GetComponent<VideoPlayer>();player.playOnAwake=false;player.isLooping=false;player.waitForFirstFrame=true;
            player.source=VideoSource.VideoClip;player.clip=clip;player.renderMode=VideoRenderMode.RenderTexture;player.targetTexture=texture;
            player.audioOutputMode=VideoAudioOutputMode.AudioSource;
            if(clip.audioTrackCount>0){player.controlledAudioTrackCount=1;player.EnableAudioTrack(0,true);player.SetTargetAudioSource(0,audio);}
            player.sendFrameReadyEvents=true;player.prepareCompleted+=OnPrepared;player.frameReady+=OnFrame;player.loopPointReached+=OnFinished;player.errorReceived+=OnError;
            lastUpdate=Time.realtimeSinceStartupAsDouble;player.Prepare();
        }
        private void OnPrepared(VideoPlayer source)
        {if(!disposed && !Failed && !paused)source.Play();}
        private void OnFrame(VideoPlayer source,long frame)
        {if(disposed || Failed)return;image.enabled=true;source.sendFrameReadyEvents=false;}
        private void OnFinished(VideoPlayer source)
        {if(disposed || Failed || Finished)return;Finished=true;source.Pause();audio.Stop();}
        private void OnError(VideoPlayer source,string message)
        {
            if(disposed || Failed)return;
            Failed=true;image.enabled=false;source.Stop();audio.Stop();
            Debug.LogError("Boss ending video failed: "+message);
        }
        internal void Tick(bool shouldPause)
        {
            if(disposed)return;
            double now=Time.realtimeSinceStartupAsDouble,delta=Math.Max(0,now-lastUpdate);lastUpdate=now;
            if(paused!=shouldPause)
            {
                paused=shouldPause;
                if(paused){player.Pause();audio.Pause();}
                else if(Busy && player.isPrepared){player.Play();audio.UnPause();}
            }
            if(Busy && !paused && !player.isPrepared)
            {preparationSeconds+=delta;if(preparationSeconds>=15)OnError(player,"Preparing the movie timed out.");}
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            player.prepareCompleted-=OnPrepared;player.frameReady-=OnFrame;player.loopPointReached-=OnFinished;player.errorReceived-=OnError;
            player.Stop();audio.Stop();player.targetTexture=null;image.texture=null;
            texture.Release();Object.Destroy(texture);Object.Destroy(root);
        }
    }
}

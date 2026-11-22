mergeInto(LibraryManager.library, {
  PPDiag_Install: function () {
    if (window.__ppDiagInstalled) return;
    window.__ppDiagInstalled = true;
    var KEY = "ppcoop_forensic_report_v1", events = [], MAX = 1200, started = Date.now();

    function str(v) {
      try { if (v instanceof Error) return v.name + ": " + v.message + "\n" + (v.stack || ""); return typeof v === "string" ? v : JSON.stringify(v); }
      catch (e) { return String(v); }
    }
    function record(kind, detail) {
      try {
        events.push({time:new Date().toISOString(), elapsedMs:Math.round(performance.now()), kind:kind, detail:str(detail)});
        if (events.length > MAX) events.splice(0, events.length - MAX);
        localStorage.setItem(KEY, JSON.stringify({schema:1, started:started, updated:Date.now(), url:location.href, userAgent:navigator.userAgent, online:navigator.onLine, events:events}));
        var s=document.getElementById("ppdiag-status"); if(s) s.textContent=kind+" — "+str(detail).slice(0,180);
      } catch(e) {}
    }
    window.__ppDiagRecord = record;
    window.__ppDiagExport = function() {
      return JSON.stringify({schema:1,started:started,exported:Date.now(),url:location.href,userAgent:navigator.userAgent,online:navigator.onLine,events:events},null,2);
    };
    record("DIAGNOSTICS_INSTALLED", {url:location.href, ua:navigator.userAgent, online:navigator.onLine, websocket:typeof WebSocket, indexedDB:typeof indexedDB, visibility:document.visibilityState, memory:navigator.deviceMemory||"unknown", cores:navigator.hardwareConcurrency||"unknown"});

    window.addEventListener("error", function(e){record("WINDOW_ERROR",{message:e.message,file:e.filename,line:e.lineno,column:e.colno,error:str(e.error)});},true);
    window.addEventListener("unhandledrejection",function(e){record("UNHANDLED_REJECTION",str(e.reason));});
    window.addEventListener("online",function(){record("ONLINE",true);});
    window.addEventListener("offline",function(){record("OFFLINE",false);});
    document.addEventListener("visibilitychange",function(){record("VISIBILITY",document.visibilityState);});
    window.addEventListener("pagehide",function(){record("PAGEHIDE",true);});
    if(window.alert) {
      var oldAlert=window.alert;
      window.alert=function(message){record("ALERT_CALLED",{message:str(message),stack:(new Error("alert caller")).stack});return oldAlert.apply(window,arguments);};
    }

    if(window.WebSocket) {
      var NativeWS=window.WebSocket;
      try {
        window.WebSocket=new Proxy(NativeWS,{
          construct:function(Target,args) {
            var url=str(args[0]), id="ws-"+Date.now()+"-"+Math.floor(Math.random()*1e6);
            record("WS_CONSTRUCT",{id:id,url:url,protocols:args.length>1?str(args[1]):"default"});
            var ws;
            try { ws=Reflect.construct(Target,args,Target); }
            catch(e){record("WS_CONSTRUCTOR_THROW",{id:id,url:url,error:str(e)});throw e;}
            record("WS_CREATED",{id:id,url:url,readyState:ws.readyState});
            ws.addEventListener("open",function(){record("WS_OPEN",{id:id,url:url,protocol:ws.protocol,extensions:ws.extensions});});
            ws.addEventListener("error",function(){record("WS_ERROR",{id:id,url:url,readyState:ws.readyState});});
            ws.addEventListener("close",function(e){record("WS_CLOSE",{id:id,url:url,code:e.code,reason:e.reason,clean:e.wasClean,readyState:ws.readyState});});
            var count=0;
            ws.addEventListener("message",function(e){count++;if(count<=12)record("WS_MESSAGE",{id:id,count:count,type:typeof e.data,size:typeof e.data==="string"?e.data.length:(e.data&&e.data.byteLength)||"unknown"});});
            return ws;
          }
        });
        record("WS_HOOK_INSTALLED",true);
      } catch(e) {record("WS_HOOK_FAILED",str(e));}
    } else record("WS_UNAVAILABLE",true);

    if(window.IDBFactory && IDBFactory.prototype.open) {
      try {
        var open=IDBFactory.prototype.open;
        IDBFactory.prototype.open=function(name,version){
          record("IDB_OPEN",{name:name,version:version||"default"});
          var req=open.apply(this,arguments);
          req.addEventListener("error",function(){record("IDB_OPEN_ERROR",{name:name,error:req.error&&req.error.message});});
          req.addEventListener("blocked",function(){record("IDB_OPEN_BLOCKED",name);});
          req.addEventListener("success",function(){record("IDB_OPEN_SUCCESS",{name:name,version:req.result.version});});
          return req;
        };
        record("IDB_HOOK_INSTALLED",true);
      } catch(e){record("IDB_HOOK_FAILED",str(e));}
    }

    function panel() {
      if(!document.body || document.getElementById("ppdiag-panel"))return;
      var p=document.createElement("div");p.id="ppdiag-panel";
      p.style.cssText="position:fixed;z-index:2147483647;left:8px;bottom:8px;max-width:95vw;background:#171717;color:white;border:2px solid #ffad42;border-radius:7px;padding:8px;font:12px sans-serif;";
      var h=document.createElement("b");h.textContent="Pumpkin Panic forensic diagnostics";p.appendChild(h);
      var s=document.createElement("div");s.id="ppdiag-status";s.textContent="Recording browser + Unity events";s.style.cssText="margin:5px 0;max-height:42px;overflow:hidden;overflow-wrap:anywhere;";p.appendChild(s);
      function btn(label,fn){var b=document.createElement("button");b.textContent=label;b.style.cssText="margin:3px;padding:7px;background:#333;color:white;border:1px solid #aaa;";b.onclick=fn;p.appendChild(b);}
      btn("Download JSON report",function(){var blob=new Blob([window.__ppDiagExport()],{type:"application/json"}),u=URL.createObjectURL(blob),a=document.createElement("a");a.href=u;a.download="pumpkin-panic-forensic-report.json";a.click();setTimeout(function(){URL.revokeObjectURL(u);},1500);record("REPORT_DOWNLOADED",true);});
      btn("Copy report",function(){var t=window.__ppDiagExport();if(navigator.clipboard&&navigator.clipboard.writeText)navigator.clipboard.writeText(t).then(function(){record("REPORT_COPIED",true);},function(e){record("COPY_FAILED",str(e));});else{var x=document.createElement("textarea");x.value=t;document.body.appendChild(x);x.select();try{document.execCommand("copy");record("REPORT_COPIED_FALLBACK",true);}catch(e){record("COPY_FAILED",str(e));}x.remove();}});
      btn("Hide",function(){p.style.display="none";});document.body.appendChild(p);
    }
    if(document.readyState==="loading")document.addEventListener("DOMContentLoaded",panel);else panel();
    record("FORENSIC_HOOKS_READY",true);
  },
  PPDiag_Log:function(ptr){if(window.__ppDiagRecord)window.__ppDiagRecord("UNITY_LOG",UTF8ToString(ptr));},
  PPDiag_Mark:function(ptr){if(window.__ppDiagRecord)window.__ppDiagRecord("UNITY_STAGE",UTF8ToString(ptr));}
});
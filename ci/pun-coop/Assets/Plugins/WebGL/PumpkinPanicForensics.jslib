mergeInto(LibraryManager.library, {
  PPDiag_Install: function () {
    if (window.__ppDiagInstalled) return;
    window.__ppDiagInstalled = true;
    var KEY = "ppcoop_forensic_report_v2", events = [], MAX = 1800, started = Date.now();
    var previous = null, storageOK = false;

    function str(v) {
      try { if (v instanceof Error) return v.name + ": " + v.message + "\n" + (v.stack || ""); return typeof v === "string" ? v : JSON.stringify(v); }
      catch (e) { return String(v); }
    }
    function persist() {
      try {
        localStorage.setItem(KEY, JSON.stringify({schema:2, started:started, updated:Date.now(), url:location.href, userAgent:navigator.userAgent, events:events}));
        storageOK = true;
      } catch(e) { storageOK = false; }
    }
    function record(kind, detail) {
      try {
        events.push({time:new Date().toISOString(), elapsedMs:Math.round(performance.now()), sessionStarted:started, kind:kind, detail:str(detail)});
        if (events.length > MAX) events.splice(0, events.length - MAX);
        persist();
        var s=document.getElementById("ppdiag-status");
        if(s) s.textContent=kind+" — "+str(detail).slice(0,220);
        var c=document.getElementById("ppdiag-count");
        if(c) c.textContent=events.length+" events captured";
      } catch(e) {}
    }
    try {
      var saved=localStorage.getItem(KEY);
      if(saved) {
        var parsed=JSON.parse(saved);
        if(parsed && parsed.schema===2 && Array.isArray(parsed.events)) {
          previous={started:parsed.started||null,updated:parsed.updated||null,count:parsed.events.length};
          events=parsed.events.slice(-MAX);
        }
      }
      storageOK=true;
    } catch(e) { storageOK=false; }
    window.__ppDiagRecord = record;
    window.__ppDiagExport = function() {
      return JSON.stringify({schema:2, reportType:"Pumpkin Panic WebGL co-op diagnostics", currentSessionStarted:started,
        exported:Date.now(),url:location.href,userAgent:navigator.userAgent,online:navigator.onLine,
        previousReportRestored:!!previous,previousReport:previous,storageAvailable:storageOK,events:events},null,2);
    };
    record("DIAGNOSTICS_SESSION_STARTED", {url:location.href, ua:navigator.userAgent, online:navigator.onLine,
      websocket:typeof WebSocket, indexedDB:typeof indexedDB, visibility:document.visibilityState,
      memory:navigator.deviceMemory||"unknown", cores:navigator.hardwareConcurrency||"unknown",
      restoredPreviousEvents:previous ? previous.count : 0});
    if(previous) record("PREVIOUS_DIAGNOSTIC_SESSION_RESTORED",previous);

    window.addEventListener("error", function(e){record("WINDOW_ERROR",{message:e.message,file:e.filename,line:e.lineno,column:e.colno,error:str(e.error)});},true);
    window.addEventListener("unhandledrejection",function(e){record("UNHANDLED_REJECTION",str(e.reason));});
    window.addEventListener("online",function(){record("ONLINE",true);});
    window.addEventListener("offline",function(){record("OFFLINE",false);});
    document.addEventListener("visibilitychange",function(){record("VISIBILITY",document.visibilityState);});
    window.addEventListener("pagehide",function(){record("PAGEHIDE",true);});
    window.addEventListener("beforeunload",function(){record("PAGE_UNLOAD",true);});
    if(window.alert) {
      var oldAlert=window.alert;
      window.alert=function(message){record("ALERT_CALLED",{message:str(message),stack:(new Error("alert caller")).stack});return oldAlert.apply(window,arguments);};
    }

    // Deliberately leave the native WebSocket constructor and its prototype untouched.
    record("WS_HOOK_DISABLED_FOR_ISOLATION", {nativeWebSocket:typeof window.WebSocket});

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

    function runChecks() {
      record("SAFE_CHECKS_STARTED","Browser-only checks; no Photon connection is attempted.");
      var result={secureContext:window.isSecureContext,webSocket:typeof WebSocket==="function",
        indexedDB:typeof indexedDB!=="undefined",online:navigator.onLine,visibility:document.visibilityState,
        userAgent:navigator.userAgent,hardwareConcurrency:navigator.hardwareConcurrency||null,
        deviceMemory:navigator.deviceMemory||null,webgl:null,localStorage:null,indexedDBTest:"not-run"};
      try {
        var testKey="__ppdiag_test_"+Date.now();
        localStorage.setItem(testKey,"ok");
        result.localStorage=localStorage.getItem(testKey)==="ok";
        localStorage.removeItem(testKey);
      } catch(e) { result.localStorage="FAILED: "+str(e); }
      try {
        var canvas=document.createElement("canvas");
        var gl=canvas.getContext("webgl2")||canvas.getContext("webgl")||canvas.getContext("experimental-webgl");
        result.webgl=gl ? {supported:true,renderer:(gl.getParameter(gl.RENDERER)||"unknown"),vendor:(gl.getParameter(gl.VENDOR)||"unknown")} : {supported:false};
      } catch(e) { result.webgl="FAILED: "+str(e); }
      if(typeof indexedDB!=="undefined") {
        try {
          var dbName="ppdiag_probe_"+Date.now(), req=indexedDB.open(dbName,1);
          req.onupgradeneeded=function(){};
          req.onsuccess=function(){try{req.result.close();indexedDB.deleteDatabase(dbName);result.indexedDBTest="PASS";}catch(e){result.indexedDBTest="CLEANUP_FAILED: "+str(e);}record("SAFE_CHECK_RESULT",result);};
          req.onerror=function(){result.indexedDBTest="FAILED: "+str(req.error);record("SAFE_CHECK_RESULT",result);};
          req.onblocked=function(){result.indexedDBTest="BLOCKED";record("SAFE_CHECK_RESULT",result);};
        } catch(e) { result.indexedDBTest="FAILED: "+str(e); record("SAFE_CHECK_RESULT",result); }
      } else { result.indexedDBTest="UNAVAILABLE"; record("SAFE_CHECK_RESULT",result); }
      record("SAFE_CHECK_BROWSER_RESULTS",result);
      var out=document.getElementById("ppdiag-results");
      if(out) out.textContent=JSON.stringify(result,null,2);
    }

    function panel() {
      if(!document.body || document.getElementById("ppdiag-panel"))return;
      var p=document.createElement("div");p.id="ppdiag-panel";
      p.style.cssText="position:fixed;z-index:2147483647;left:8px;bottom:8px;width:min(440px,calc(100vw - 32px));max-height:75vh;overflow:auto;background:#171717;color:#fff;border:2px solid #ffad42;border-radius:9px;padding:10px;font:13px/1.4 Arial,sans-serif;box-shadow:0 3px 18px #0009;";
      var h=document.createElement("div");h.textContent="Pumpkin Panic — Diagnostics";h.style.cssText="font-weight:bold;font-size:15px;margin-bottom:6px;";p.appendChild(h);
      var s=document.createElement("div");s.id="ppdiag-status";s.textContent="Recording browser + Unity events";s.style.cssText="margin:5px 0;max-height:54px;overflow:hidden;overflow-wrap:anywhere;color:#ffd18c;";p.appendChild(s);
      var count=document.createElement("div");count.id="ppdiag-count";count.textContent=events.length+" events captured";count.style.cssText="color:#bbb;font-size:11px;";p.appendChild(count);
      var hint=document.createElement("div");hint.textContent="Run safe checks first. These do NOT connect to Photon. Then use the game's Connect to Photon button and export the report, even if it fails.";hint.style.cssText="margin:7px 0;color:#ddd;";p.appendChild(hint);
      function btn(label,fn){var b=document.createElement("button");b.textContent=label;b.style.cssText="margin:3px 3px 3px 0;padding:7px 9px;background:#333;color:white;border:1px solid #aaa;border-radius:4px;cursor:pointer;";b.onclick=fn;p.appendChild(b);return b;}
      btn("Run safe checks",runChecks);
      btn("Mark ready to test Photon",function(){record("USER_MARKED_PHOTON_TEST_READY","Safe checks reviewed; use the in-game Connect to Photon button when ready.");});
      btn("Download JSON",function(){var blob=new Blob([window.__ppDiagExport()],{type:"application/json"}),u=URL.createObjectURL(blob),a=document.createElement("a");a.href=u;a.download="pumpkin-panic-diagnostics.json";a.click();setTimeout(function(){URL.revokeObjectURL(u);},1500);record("REPORT_DOWNLOADED",true);});
      btn("Copy report",function(){var t=window.__ppDiagExport();if(navigator.clipboard&&navigator.clipboard.writeText)navigator.clipboard.writeText(t).then(function(){record("REPORT_COPIED",true);},function(e){record("COPY_FAILED",str(e));});else{var x=document.createElement("textarea");x.value=t;document.body.appendChild(x);x.select();try{document.execCommand("copy");record("REPORT_COPIED_FALLBACK",true);}catch(e){record("COPY_FAILED",str(e));}x.remove();}});
      btn("Hide",function(){p.style.display="none";});
      var results=document.createElement("pre");results.id="ppdiag-results";results.textContent="Safe checks have not been run.";results.style.cssText="white-space:pre-wrap;overflow-wrap:anywhere;background:#242424;padding:7px;border-radius:4px;max-height:150px;overflow:auto;color:#bfe3ff;font-size:11px;";p.appendChild(results);
      document.body.appendChild(p);
      var reopen=document.createElement("button");reopen.id="ppdiag-reopen";reopen.textContent="Diagnostics";reopen.style.cssText="position:fixed;z-index:2147483647;left:8px;bottom:8px;padding:8px;background:#171717;color:#fff;border:2px solid #ffad42;border-radius:6px;display:none;";reopen.onclick=function(){p.style.display="block";reopen.style.display="none";};document.body.appendChild(reopen);
      var hide=p.querySelector("button:last-of-type");hide.onclick=function(){p.style.display="none";reopen.style.display="block";};
      record("DIAGNOSTICS_PANEL_READY",{restoredPreviousReport:!!previous,events:events.length});
    }
    if(document.readyState==="loading")document.addEventListener("DOMContentLoaded",panel);else panel();
    record("FORENSIC_HOOKS_READY",true);
  },
  PPDiag_Log:function(ptr){if(window.__ppDiagRecord)window.__ppDiagRecord("UNITY_LOG",UTF8ToString(ptr));},
  PPDiag_Mark:function(ptr){if(window.__ppDiagRecord)window.__ppDiagRecord("UNITY_STAGE",UTF8ToString(ptr));}
});

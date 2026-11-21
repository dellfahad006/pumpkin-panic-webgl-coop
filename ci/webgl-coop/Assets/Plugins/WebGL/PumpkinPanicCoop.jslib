mergeInto(LibraryManager.library, {
  PPWS_Connect: function(urlPtr, gameObjectPtr) {
    var url = UTF8ToString(urlPtr);
    var gameObject = UTF8ToString(gameObjectPtr);
    try {
      if (Module.ppCoopSocket) {
        try { Module.ppCoopSocket.close(); } catch (e) {}
      }
      var socket = new WebSocket(url);
      Module.ppCoopSocket = socket;
      socket.onopen = function() { SendMessage(gameObject, 'OnSocketOpen', ''); };
      socket.onmessage = function(event) {
        if (typeof event.data === 'string') SendMessage(gameObject, 'OnSocketMessage', event.data);
      };
      socket.onerror = function() { SendMessage(gameObject, 'OnSocketError', 'WebSocket connection failed'); };
      socket.onclose = function(event) { SendMessage(gameObject, 'OnSocketClosed', event.reason || 'Disconnected'); };
    } catch (e) {
      SendMessage(gameObject, 'OnSocketError', String(e));
    }
  },
  PPWS_Send: function(messagePtr) {
    var socket = Module.ppCoopSocket;
    if (socket && socket.readyState === WebSocket.OPEN) socket.send(UTF8ToString(messagePtr));
  },
  PPWS_Close: function() {
    var socket = Module.ppCoopSocket;
    if (socket) { try { socket.close(1000, 'Leaving'); } catch (e) {} }
    Module.ppCoopSocket = null;
  }
});

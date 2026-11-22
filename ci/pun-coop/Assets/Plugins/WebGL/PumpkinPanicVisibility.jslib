mergeInto(LibraryManager.library, {
  PPVisibility_Register: function (gameObjectNamePtr) {
    var name = UTF8ToString(gameObjectNamePtr);
    if (window.__ppVisibilityHandler) document.removeEventListener('visibilitychange', window.__ppVisibilityHandler);
    window.__ppVisibilityHandler = function () {
      var visible = document.visibilityState === 'visible' ? '1' : '0';
      if (typeof SendMessage === 'function') SendMessage(name, 'OnBrowserVisibilityChanged', visible);
    };
    document.addEventListener('visibilitychange', window.__ppVisibilityHandler);
    window.__ppVisibilityHandler();
  }
});

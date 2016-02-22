﻿using System;
using MVVM.HTML.Core.JavascriptEngine;
using MVVM.HTML.Core.JavascriptEngine.JavascriptObject;

namespace MVVM.HTML.Core.Window
{
    /// <summary>
    /// HTML Window abstraction
    /// </summary>
    public interface IHTMLWindow
    {
        /// <summary>
        /// Get the main frame logic
        /// </summary>
        IWebView MainFrame { get; }

        /// <summary>
        /// Navigate to the specified path
        /// </summary>
        /// <param name="path">
        /// Path to navigate to
        /// </param>
        void NavigateTo(string path);

        /// <summary>
        /// Url of the browser
        /// </summary>
        string Url { get; }

        /// <summary>
        /// true if the content is loaded and ready
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// event fired when the window is loaded
        /// </summary>
        event EventHandler<LoadEndEventArgs> LoadEnd;

        /// <summary>
        /// event fired when the console log is called in the browser
        /// </summary>
        event EventHandler<ConsoleMessageArgs> ConsoleMessage;

        /// <summary>
        /// event fired when browser process crashed
        /// </summary>
        event EventHandler<BrowserCrashedArgs> Crashed;
    }
}
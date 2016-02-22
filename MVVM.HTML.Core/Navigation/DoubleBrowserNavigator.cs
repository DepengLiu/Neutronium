﻿using System;
using System.Threading.Tasks;
using System.Diagnostics;

using MVVM.HTML.Core.Navigation.Window;
using MVVM.HTML.Core.Navigation;
using MVVM.HTML.Core.Infra;
using MVVM.HTML.Core.Exceptions;
using MVVM.HTML.Core.Window;
using MVVM.HTML.Core.JavascriptEngine;
using MVVM.HTML.Core.Binding;
using MVVM.HTML.Core.Binding.Mapping;
using MVVM.HTML.Core.JavascriptEngine.Window;
using MVVM.HTML.Core.JavascriptUIFramework;

namespace MVVM.HTML.Core
{
    public class DoubleBrowserNavigator : INavigationSolver
    {
        private readonly IWebViewLifeCycleManager _WebViewLifeCycleManager;
        private readonly IJavascriptSessionInjectorFactory _JavascriptSessionInjectorFactory;
        private readonly IUrlSolver _UrlSolver;        
        private IHTMLWindowProvider _CurrentWebControl;
        private IHTMLWindowProvider _NextWebControl;
        private IHTMLBinding _HTMLBinding;
        private IWebSessionWatcher _IWebSessionWatcher = new NullWatcher();
        private string _Url;
        private bool _Disposed = false;
        private bool _Navigating = false;
        private HTMLLogicWindow _Window;

        public string Url { get { return _Url; } }
        public IHTMLWindowProvider WebControl { get { return _CurrentWebControl; } }


        public IWebSessionWatcher WebSessionWatcher
        {
            set { _IWebSessionWatcher = value; }
        }

        public DoubleBrowserNavigator(IWebViewLifeCycleManager lifecycler, IUrlSolver urlSolver, 
                                        IJavascriptSessionInjectorFactory javascriptSessionInjectorFactory)
        {
            _JavascriptSessionInjectorFactory = javascriptSessionInjectorFactory;
            _WebViewLifeCycleManager = lifecycler;
            _UrlSolver = urlSolver;
        }

        private void ConsoleMessage(object sender, ConsoleMessageArgs e)
        { 
            try
            {
                LogBrowser(string.Format("{0}, source {1}, line number {2}, page {3}", e.Message, e.Source, e.Line, _Url));
            }
            catch { }
        }

        private IHTMLBinding Binding
        {
            get { return _HTMLBinding; }
            set
            {
                if (_HTMLBinding != null)
                    _HTMLBinding.Dispose();

                _HTMLBinding = value;

                if (_Disposed && (_HTMLBinding!=null))
                   Binding = null;
            }
        }

        private void FireNavigate(object inewvm, object ioldvm=null) 
        {
            if (OnNavigate != null)
                OnNavigate(this, new NavigationEvent(inewvm, ioldvm));
        }

        private void FireLoaded(object iloadedvm)
        {
            if (OnDisplay != null)
                OnDisplay(this, new DisplayEvent(iloadedvm));
        }

        private void Switch(Task<IHTMLBinding> iBinding, HTMLLogicWindow iwindow, TaskCompletionSource<IHTMLBinding> tcs)
        {
            object oldvm = (Binding != null) ? Binding.Root : null;
            Binding = iBinding.Result;
          
            if (_CurrentWebControl!=null)
            {
                _CurrentWebControl.HTMLWindow.ConsoleMessage -= ConsoleMessage;
                _CurrentWebControl.Dispose();
            }
            else if (OnFirstLoad != null)
                OnFirstLoad(this, EventArgs.Empty);

            _CurrentWebControl = _NextWebControl;     
            _NextWebControl = null;
            _CurrentWebControl.HTMLWindow.Crashed += Crashed;

            _CurrentWebControl.Show();
    
            _Window = iwindow; 

            var inav = _UseINavigable ? Binding.Root as INavigable : null;
            if (inav != null)
                inav.Navigation = this;
            _Window.State = WindowLogicalState.Opened;

            _Window.OpenAsync().ContinueWith(t => EndAnimation(Binding.Root));

            _Navigating = false;
           
            FireNavigate(Binding.Root, oldvm);
            
            if (tcs != null) tcs.SetResult(Binding);
        }    

        private void EndAnimation(object inavgable)
        {
            _WebViewLifeCycleManager.GetDisplayDispatcher()
                .RunAsync( () => FireLoaded(inavgable) );
        }

        private void LogCritical(string iMessage)
        {
            _IWebSessionWatcher.LogCritical(iMessage);
            Trace.WriteLine(string.Format("MVVM for CEFGlue: Critical: {0}", iMessage));
        }

        private void LogBrowser(string iMessage)
        {
            _IWebSessionWatcher.LogBrowser(iMessage);
            Trace.WriteLine(string.Format("MVVM for CEFGlue: WebSession log message: {0}", iMessage));
        }

        private void Crashed(object sender, BrowserCrashedArgs e)
        {
            var dest = _CurrentWebControl.HTMLWindow.Url;
            var vm = Binding.Root;
            var mode = Binding.Mode;

            LogCritical("WebView crashed trying recover");

            CleanWebControl(ref _CurrentWebControl);
            Binding = null;

            Navigate(dest, vm, mode);
        }

        private Task Navigate(string iUri, object iViewModel, JavascriptBindingMode iMode = JavascriptBindingMode.TwoWay)
        {
            if (iUri == null)
                throw ExceptionHelper.GetArgument(string.Format("ViewModel not registered: {0}", iViewModel.GetType()));

            _Navigating = true;

            INavigable oldvm = (Binding != null) ? Binding.Root as INavigable : null;

            if (_UseINavigable && (oldvm!=null))
            {
                oldvm.Navigation = null;
            }

            var wh = new WindowHelper(new HTMLLogicWindow());

            if (_CurrentWebControl != null)
                _CurrentWebControl.HTMLWindow.Crashed -= Crashed;

            Task closetask = ( _CurrentWebControl!=null) ? _Window.CloseAsync() : TaskHelper.Ended();

            _NextWebControl = _WebViewLifeCycleManager.Create();
            _NextWebControl.HTMLWindow.ConsoleMessage += ConsoleMessage;

            var moderWindow = _NextWebControl.HTMLWindow as IHTMLModernWindow;
            if (moderWindow!=null)
            {
                EventHandler<BeforeJavascriptExcecutionArgs> before = null;
                before = (o,e) =>
                {
                    moderWindow.BeforeJavascriptExecuted -= before;
                    _JavascriptSessionInjectorFactory.ExcecuteFirst(e.JavascriptExecutor);
                };
                moderWindow.BeforeJavascriptExecuted += before;
            }

            var tcs = new TaskCompletionSource<IHTMLBinding>();

            EventHandler<LoadEndEventArgs> sourceupdate = null;
            sourceupdate = (o, e) =>
            {
                var injectorFactory = GetInjectorFactory(iUri);
                _NextWebControl.HTMLWindow.LoadEnd -= sourceupdate;
                var engine = new HTMLViewEngine(_NextWebControl, injectorFactory);

                HTML_Binding.Bind(engine, iViewModel, iMode, wh).WaitWith(closetask, t => Switch(t, wh.__window__, tcs));
            };

            _Url = iUri;
            _NextWebControl.HTMLWindow.LoadEnd += sourceupdate;
            _NextWebControl.HTMLWindow.NavigateTo(iUri);

            return tcs.Task;
        }

        private IJavascriptSessionInjectorFactory GetInjectorFactory(string iUri)
        {
            return _JavascriptSessionInjectorFactory;
        }

        public void ExcecuteJavascript(string icode)
        {
            try 
            {
                _CurrentWebControl.HTMLWindow.MainFrame.ExecuteJavaScript(icode);
            }
            catch(Exception e)
            {
                LogBrowser(string.Format("Can not execute javascript: {0}, reason: {1}",icode,e));
            }          
        }

        public async Task NavigateAsync(object iViewModel, string Id = null,JavascriptBindingMode iMode = JavascriptBindingMode.TwoWay)
        {
            if ((iViewModel == null) || (_Navigating))
                return;

            var viewPath = _UrlSolver.Solve(iViewModel, Id);
            if (viewPath == null)
                throw ExceptionHelper.Get(string.Format("Unable to locate ViewModel {0}", iViewModel));

            await Navigate(viewPath.LocalPath, iViewModel, iMode);
        }

        public void Dispose()
        {
            _Disposed = true;
            Binding = null;
            UseINavigable = false;

            CleanWebControl(ref _CurrentWebControl);
            CleanWebControl(ref _NextWebControl);
        }

        private void CleanWebControl(ref IHTMLWindowProvider iWebControl)
        {
            if (iWebControl == null)
                return;

            iWebControl.HTMLWindow.Crashed -= Crashed;
            iWebControl.HTMLWindow.ConsoleMessage -= ConsoleMessage;
            iWebControl.Dispose();
            iWebControl = null;
        }

        private bool _UseINavigable = false;
        public bool UseINavigable
        {
            get { return _UseINavigable; }
            set { _UseINavigable = value; }
        }

        public event EventHandler<NavigationEvent> OnNavigate;

        public event EventHandler<DisplayEvent> OnDisplay;

        public event EventHandler OnFirstLoad;
    }
}
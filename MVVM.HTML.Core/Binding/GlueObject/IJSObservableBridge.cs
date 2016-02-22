﻿using MVVM.HTML.Core.Binding.Mapping;
using MVVM.HTML.Core.JavascriptEngine.JavascriptObject;

namespace MVVM.HTML.Core.HTMLBinding
{
    public interface IJSObservableBridge : IJSCSGlue
    {
        IJavascriptObject MappedJSValue { get; }
        void SetMappedJSValue(IJavascriptObject ijsobject);
    }
}
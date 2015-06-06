// IMvxLayoutInfactorFactory.cs
// (c) Copyright Cirrious Ltd. http://www.cirrious.com
// MvvmCross is licensed using Microsoft Public License (Ms-PL)
// Contributions and inspirations noted in readme.md and license.txt
// 
// Project Lead - Stuart Lodge, @slodge, me@slodge.com

using System;
using System.Collections.Generic;
using Android.Content;
using Android.Util;
using Android.Views;
using Cirrious.MvvmCross.Binding.Bindings;

namespace Cirrious.MvvmCross.Binding.Droid.Binders
{
    public interface IMvxLayoutInflaterHolderFactory : IMvxLayoutInflaterFactory
    {
        IList<KeyValuePair<object, IMvxUpdateableBinding>> CreatedBindings { get; }

        // I added this as a convenience but this class is going to need some love.
        View BindView(View view, Context context, IAttributeSet attrs);
    }

    public interface IMvxLayoutInflaterFactory
    {
        View OnCreateView(View parent, string name, Context context, IAttributeSet attrs);
    }
}
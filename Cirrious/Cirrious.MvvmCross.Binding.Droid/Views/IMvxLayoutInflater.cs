// IMvxLayoutInflater.cs
// (c) Copyright Cirrious Ltd. http://www.cirrious.com
// MvvmCross is licensed using Microsoft Public License (Ms-PL)
// Contributions and inspirations noted in readme.md and license.txt
// 
// Project Lead - Stuart Lodge, @slodge, me@slodge.com

using System;
using System.Collections.Generic;
using System.Xml;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Cirrious.CrossCore;
using Cirrious.MvvmCross.Binding.BindingContext;
using Cirrious.MvvmCross.Binding.Bindings;
using Cirrious.MvvmCross.Binding.Droid.Binders;
using Java.Lang;
using Java.Lang.Reflect;

namespace Cirrious.MvvmCross.Binding.Droid.Views
{
    public interface IMvxLayoutInflater
    {
        LayoutInflater LayoutInflater { get; }
    }

    public class MvxBindingFactoryPlaceholder
    {
        private const string Tag = "MvxBindingFactoryPlaceholder";

        public View OnViewCreated(View view, Context context, IAttributeSet attrs)
        {
            if (view != null)
            {
                // Bind here.
                Mvx.TaggedTrace(Tag, "Binding {0}", view.ToString());
            }

            return view;
        }
    }

    public class MvxLayoutInflater : LayoutInflater
    {
        private IMvxBindingContextOwner _bindingContextOwner;
        private IMvxAndroidViewFactory _androidViewFactory;

        private readonly MvxBindingFactoryPlaceholder _bindingFactoryPlaceholder;
        private bool _setPrivateFactory;

        public MvxLayoutInflater(Context context)
            : base(context)
        {
            this._bindingFactoryPlaceholder = new MvxBindingFactoryPlaceholder();
            SetupLayoutFactories(false);
        }

        public MvxLayoutInflater(LayoutInflater original, Context newContext, IMvxBindingContextOwner bindingContextOwner, bool cloned)
            : base(original, newContext)
        {
            this._bindingContextOwner = bindingContextOwner;

            this._bindingFactoryPlaceholder = new MvxBindingFactoryPlaceholder();

            SetupLayoutFactories(cloned);
        }

        public override LayoutInflater CloneInContext(Context newContext)
        {
            return new MvxLayoutInflater(this, newContext, this._bindingContextOwner, true);
        }

        //public override View Inflate(XmlReader parser, ViewGroup root, bool attachToRoot)
        //{
        //    //SetPrivateFactoryInternal();
        //    return base.Inflate(parser, root, attachToRoot);
        //}

        protected override View OnCreateView(View parent, string name, IAttributeSet attrs)
        {
            return this._bindingFactoryPlaceholder.OnViewCreated(base.OnCreateView(parent, name, attrs), this.Context, attrs);
        }

        protected override View OnCreateView(string name, IAttributeSet attrs)
        {
            View view = AndroidViewFactory.CreateView(null, name, this.Context, attrs);
            if (view == null)
                view = base.OnCreateView(name, attrs);
            return this._bindingFactoryPlaceholder.OnViewCreated(view, view.Context, attrs);
        }

        private void SetPrivateFactoryInternal()
        {
            if (_setPrivateFactory)
                return;

            if (Build.VERSION.SdkInt < BuildVersionCodes.Honeycomb)
                return;

            if (!(this.Context is IFactory2))
            {
                _setPrivateFactory = true;
                return;
            }

            Class layoutInflaterClass = Class.FromType(typeof(LayoutInflater));
            Method setPrivateFactoryMethod = layoutInflaterClass.GetMethod("setPrivateFactory", Class.FromType(typeof(IFactory2)));
            if (setPrivateFactoryMethod != null)
            {
                try
                {
                    setPrivateFactoryMethod.Accessible = true;
                    setPrivateFactoryMethod.Invoke(this,
                        new PrivateFactoryWrapper2((IFactory2)Context, this, this._bindingFactoryPlaceholder));
                }
                catch(Java.Lang.Exception ex)
                {
                    Mvx.Warning("Cannot invoke LayoutInflater.setPrivateFactory :\n{0}", ex.StackTrace);
                }
            }

            _setPrivateFactory = true;
        }

        protected IMvxAndroidViewFactory AndroidViewFactory
        {
            get
            {
                return this._androidViewFactory ?? (this._androidViewFactory = Mvx.Resolve<IMvxAndroidViewFactory>());
            }
        }

        public class LayoutInflaterFactoryHack : IMvxLayoutInfactorFactory
        {
            private readonly IFactory2 _factory2;
            private readonly MvxBindingFactoryPlaceholder _factoryPlaceholder;

            public IList<KeyValuePair<object, IMvxUpdateableBinding>> CreatedBindings { get; private set; }

            public LayoutInflaterFactoryHack(IFactory2 factory2, MvxBindingFactoryPlaceholder factoryPlaceholder)
            {
                this._factory2 = factory2;
                this._factoryPlaceholder = factoryPlaceholder;
            }

            public View OnCreateView(View parent, string name, Context context, IAttributeSet attrs)
            {
                return _factoryPlaceholder.OnViewCreated(
                    _factory2.OnCreateView(parent, name, context, attrs),
                    context, attrs);
            }
        }

        private void SetupLayoutFactories(bool cloned)
        {
            if (cloned)
                return;

            if (Factory2 != null && !(Factory2 is MvxLayoutInfactorFactory.FactoryWrapper2))
            {
                MvxLayoutInfactorFactory.SetFactory(this, new LayoutInflaterFactoryHack(Factory2, _bindingFactoryPlaceholder));                
            }


        }

        private class PrivateFactoryWrapper2 : Java.Lang.Object, IFactory2
        {
            private readonly IFactory2 _factory2;
            private readonly MvxBindingFactoryPlaceholder _factoryPlaceholder;
            private readonly MvxLayoutInflater _inflater;

            internal PrivateFactoryWrapper2(IFactory2 factory2, MvxLayoutInflater inflater,
                MvxBindingFactoryPlaceholder factoryPlaceholder)
            {
                _factory2 = factory2;
                _inflater = inflater;
                _factoryPlaceholder = factoryPlaceholder;
            }

            public View OnCreateView(string name, Context context, IAttributeSet attrs)
            {
                return _factoryPlaceholder.OnViewCreated(
                    _factory2.OnCreateView(name, context, attrs),
                    context, attrs);
            }

            public View OnCreateView(View parent, string name, Context context, IAttributeSet attrs)
            {
                // Yo! This isn't what calligraphy does
                return _factoryPlaceholder.OnViewCreated(
                    _factory2.OnCreateView(parent, name, context, attrs),
                    context, attrs);
            }
        }
    }

    public class MvxContextWrapper : ContextWrapper
    {
        private LayoutInflater _inflater;
        private readonly IMvxBindingContextOwner _bindingContextOwner;

        public static ContextWrapper Wrap(Context @base, IMvxBindingContextOwner bindingContextOwner)
        {
            return new MvxContextWrapper(@base, bindingContextOwner);
        }

        protected MvxContextWrapper(Context context, IMvxBindingContextOwner bindingContextOwner)
            : base(context)
        {
            _bindingContextOwner = bindingContextOwner;
        }

        public override Java.Lang.Object GetSystemService(string name)
        {
            if (name.Equals(LayoutInflaterService))
            {
                return this._inflater ??
                       (
                       //this._inflater = LayoutInflater.FromContext(BaseContext)
                new MvxLayoutInflater(LayoutInflater.From(BaseContext), this, this._bindingContextOwner, false)
                );
            }

            return base.GetSystemService(name);
        }
    }
}
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
using Cirrious.MvvmCross.Binding.Droid.BindingContext;
using Java.Interop;
using Java.Lang;
using Java.Lang.Reflect;

namespace Cirrious.MvvmCross.Binding.Droid.Views
{
    public interface IMvxLayoutInflaterHolder
    {
        LayoutInflater LayoutInflater { get; }
    }

    public class MvxBinderPlaceholder
    {
        private IMvxLayoutInflaterHolderFactory _factory;
        private const string Tag = "MvxBindingFactoryPlaceholder";

        public IMvxLayoutInflaterHolderFactory Factory
        {
            get { return this._factory; }
            set
            {
                this._factory = value;
                Mvx.TaggedTrace(Tag, "Factory set to {0}", value == null ? "null" : value.ToString());
            }
        }

        public View OnViewCreated(View view, Context context, IAttributeSet attrs)
        {
            if ( Factory != null && view != null && view.GetTag(Resource.Id.MvvmCrossTagId) != Java.Lang.Boolean.True)
            {
                Mvx.TaggedTrace(Tag, "Binding {0}", view.ToString());

                // Bind here.
                view = Factory.BindView(view, context, attrs);

                view.SetTag(Resource.Id.MvvmCrossTagId, Java.Lang.Boolean.True);
            }

            return view;
        }
    }

    public class MvxLayoutInflater : LayoutInflater
    {
        internal static BuildVersionCodes Sdk = Build.VERSION.SdkInt;

        private IMvxAndroidViewFactory _androidViewFactory;
        private IMvxLayoutInflaterHolderFactoryFactory _layoutInflaterHolderFactoryFactory;

        private readonly MvxBinderPlaceholder _binderPlaceholder;
        private bool _setPrivateFactory;

        private Field _constructorArgs;

        public void SetCurrentFactory(IMvxLayoutInflaterHolderFactory factory)
        {
            this._binderPlaceholder.Factory = factory;
        }

        public MvxLayoutInflater(Context context)
            : base(context)
        {
            this._binderPlaceholder = new MvxBinderPlaceholder();
            SetupLayoutFactories(false);
        }

        public MvxLayoutInflater(LayoutInflater original, Context newContext, bool cloned)
            : base(original, newContext)
        {
            this._binderPlaceholder = new MvxBinderPlaceholder();

            SetupLayoutFactories(cloned);
        }

        public override LayoutInflater CloneInContext(Context newContext)
        {
            return new MvxLayoutInflater(this, newContext, true);
        }

        //public override View Inflate(XmlReader parser, ViewGroup root, bool attachToRoot)
        //{
        //    SetPrivateFactoryInternal();
        //    return base.Inflate(parser, root, attachToRoot);
        //}

        // Calligraphy doesn't override this one...
        public override View Inflate(int resource, ViewGroup root, bool attachToRoot)
        {
            SetPrivateFactoryInternal();

            try
            {
                IMvxLayoutInflaterHolderFactory factory = null;

                // Get the current binding context
                var currentBindingContext = MvxAndroidBindingContextHelpers.Current();
                if (currentBindingContext != null)
                {
                    factory =
                        ((MvxAndroidBindingContext)currentBindingContext).FactoryFactory.Create(
                            currentBindingContext.DataContext);

                    // Set the current factory used to generate bindings
                    if (factory != null)
                        this._binderPlaceholder.Factory = factory;
                }

                var view = base.Inflate(resource, root, attachToRoot);


                if (currentBindingContext != null)
                {
                    if (factory != null)
                        currentBindingContext.RegisterBindingsWithClearKey(view, factory.CreatedBindings);
                }

                return view;
            }
            finally
            {
                this._binderPlaceholder.Factory = null;
            }

            return null;
        }

        protected override View OnCreateView(View parent, string name, IAttributeSet attrs)
        {
            return this._binderPlaceholder.OnViewCreated(base.OnCreateView(parent, name, attrs), this.Context, attrs);
        }

        protected override View OnCreateView(string name, IAttributeSet attrs)
        {
            View view = AndroidViewFactory.CreateView(null, name, this.Context, attrs);
            if (view == null)
                view = base.OnCreateView(name, attrs);
            return this._binderPlaceholder.OnViewCreated(view, view.Context, attrs);
        }

        // Note: setFactory/setFactory2 are implemented with export
        // because there's a bug in the generator that doesn't
        // mark the Factory/Factory2 setters as virtual.
        // See: https://bugzilla.xamarin.com/show_bug.cgi?id=30764
        [Export]
        public void setFactory(IFactory factory)
        {
            // Wrap the incoming factory if we need to.
            if (!(factory is MvxLayoutInflaterCompat.FactoryWrapper))
            {
                base.Factory =
                    new MvxLayoutInflaterCompat.FactoryWrapper(new DelegateFactory1(factory, this._binderPlaceholder));
                return;
            }

            base.Factory = factory;
        }

        
        [Export]
        public void setFactory2(IFactory2 factory2)
        {
            // Wrap the incoming factory if we need to.
            if (!(factory2 is MvxLayoutInflaterCompat.FactoryWrapper2))
            {
                base.Factory2 =
                    new MvxLayoutInflaterCompat.FactoryWrapper2(new DelegateFactory2(factory2, this._binderPlaceholder));
                return;
            }
            base.Factory2 = factory2;
        }

        private void SetupLayoutFactories(bool cloned)
        {
            if (cloned)
                return;

            // If factories are already set we need to wrap them in our
            // own secret sauce.
            if (Sdk > BuildVersionCodes.Honeycomb)
            {
                if (Factory2 != null && !(Factory2 is MvxLayoutInflaterCompat.FactoryWrapper2)) // Check for FactoryWrapper2 may be too loose
                {
                    MvxLayoutInflaterCompat.SetFactory(this, new DelegateFactory2(Factory2, _binderPlaceholder));
                }
            }

            if (Factory != null && !(Factory is MvxLayoutInflaterCompat.FactoryWrapper)) // Check for FactoryWrapper may be too loose
            {
                MvxLayoutInflaterCompat.SetFactory(this, new DelegateFactory1(Factory, _binderPlaceholder));
            }
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
                        new PrivateFactoryWrapper2((IFactory2)Context, this, this._binderPlaceholder));
                }
                catch(Java.Lang.Exception ex)
                {
                    Mvx.Warning("Cannot invoke LayoutInflater.setPrivateFactory :\n{0}", ex.StackTrace);
                }
            }

            _setPrivateFactory = true;
        }

        protected View CreateCustomViewInternal(View parent, View view, string name, Context viewContext, IAttributeSet attrs)
        {
            if (view == null && name.IndexOf('.') > -1)
            {
                if (_constructorArgs == null)
                {
                    Class layoutInflaterClass = Class.FromType(typeof(LayoutInflater));
                    _constructorArgs = layoutInflaterClass.GetDeclaredField("mConstructorArgs");
                    _constructorArgs.Accessible = true;
                }

                Java.Lang.Object[] constructorArgsArr = (Java.Lang.Object[])_constructorArgs.Get(this);
                Java.Lang.Object lastContext = constructorArgsArr[0];

                // The LayoutInflater actually finds out the correct context to use. We just need to set
                // it on the mConstructor for the internal method.
                // Set the constructor args up for the createView, not sure why we can't pass these in.
                constructorArgsArr[0] = viewContext;
                _constructorArgs.Set(this, constructorArgsArr);
                try
                {
                    view = CreateView(name, null, attrs);
                }
                catch (ClassNotFoundException ignored) {}
                finally
                {
                    constructorArgsArr[0] = lastContext;
                    _constructorArgs.Set(this, constructorArgsArr);
                }
            }
            return view;
        }

        protected IMvxAndroidViewFactory AndroidViewFactory
        {
            get
            {
                return this._androidViewFactory ?? (this._androidViewFactory = Mvx.Resolve<IMvxAndroidViewFactory>());
            }
        }

        protected IMvxLayoutInflaterHolderFactoryFactory FactoryFactory
        {
            get
            {
                if (this._layoutInflaterHolderFactoryFactory == null)
                    this._layoutInflaterHolderFactoryFactory = Mvx.Resolve<IMvxLayoutInflaterHolderFactoryFactory>();
                return this._layoutInflaterHolderFactoryFactory;
            }
        }

        public class LayoutInflaterFactoryHack : IMvxLayoutInflaterHolderFactory
        {
            private readonly IFactory2 _factory2;
            private readonly MvxBinderPlaceholder _factoryPlaceholder;

            public IList<KeyValuePair<object, IMvxUpdateableBinding>> CreatedBindings { get; private set; }

            public LayoutInflaterFactoryHack(IFactory2 factory2, MvxBinderPlaceholder factoryPlaceholder)
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

            public View BindView(View view, Context context, IAttributeSet attrs)
            {
                // Dummy, refactor this crap.
                return null;
            }
        }

        private class DelegateFactory2 : IMvxLayoutInflaterFactory
        {
            private readonly IFactory2 _factory;
            private readonly MvxBinderPlaceholder _factoryPlaceholder;

            public DelegateFactory2(IFactory2 factoryToWrap, MvxBinderPlaceholder binder)
            {
                _factory = factoryToWrap;
                _factoryPlaceholder = binder;
            }

            public View OnCreateView(View parent, string name, Context context, IAttributeSet attrs)
            {
                return _factoryPlaceholder.OnViewCreated(
                    _factory.OnCreateView(parent, name, context, attrs),
                    context, attrs);
            }
        }

        private class DelegateFactory1 : IMvxLayoutInflaterFactory
        {
            private readonly IFactory _factory;
            private readonly MvxBinderPlaceholder _factoryPlaceholder;

            public DelegateFactory1(IFactory factoryToWrap, MvxBinderPlaceholder binder)
            {
                _factory = factoryToWrap;
                _factoryPlaceholder = binder;
            }

            public View OnCreateView(View parent, string name, Context context, IAttributeSet attrs)
            {
                return _factoryPlaceholder.OnViewCreated(
                    _factory.OnCreateView(name, context, attrs),
                    context, attrs);
            }
        }

        //private class DelegateFactory : Java.Lang.Object, IFactory
        //{
        //    private readonly IFactory _factory;
        //    private readonly MvxLayoutInflater _layoutInflater;
        //    private readonly MvxBinderPlaceholder _factoryPlaceholder;

        //    public DelegateFactory(IFactory factory, MvxLayoutInflater layoutInflater, MvxBinderPlaceholder factoryPlaceholder)
        //    {
        //        this._factory = factory;
        //        this._layoutInflater = layoutInflater;
        //        this._factoryPlaceholder = factoryPlaceholder;
        //    }

        //    public View OnCreateView(string name, Context context, IAttributeSet attrs)
        //    {
        //        if (MvxLayoutInflater.Sdk > BuildVersionCodes.Honeycomb)
        //        {
        //            return _factoryPlaceholder.OnViewCreated(
        //                _factory.OnCreateView(name, context, attrs),
        //                context, attrs);
        //        }
        //        throw new NotImplementedException("Anything before the Honeycomb hideout deserves to be burned.");
        //    }
        //}

        //private class DelegateFactory2 : Java.Lang.Object, IFactory2
        //{
        //    private readonly IFactory2 _factory2;
        //    private readonly MvxBinderPlaceholder _factoryPlaceholder;

        //    public DelegateFactory2(IFactory2 factory2, MvxBinderPlaceholder factoryPlaceholder)
        //    {
        //        this._factory2 = factory2;
        //        this._factoryPlaceholder = factoryPlaceholder;
        //    }

        //    public View OnCreateView(string name, Context context, IAttributeSet attrs)
        //    {
        //        return _factoryPlaceholder.OnViewCreated(
        //            _factory2.OnCreateView(name, context, attrs),
        //            context, attrs);
        //    }

        //    public View OnCreateView(View parent, string name, Context context, IAttributeSet attrs)
        //    {
        //        return _factoryPlaceholder.OnViewCreated(
        //            _factory2.OnCreateView(parent, name, context, attrs),
        //            context, attrs);
        //    }
        //}

        private class PrivateFactoryWrapper2 : Java.Lang.Object, IFactory2
        {
            private readonly IFactory2 _factory2;
            private readonly MvxBinderPlaceholder _factoryPlaceholder;
            private readonly MvxLayoutInflater _inflater;

            internal PrivateFactoryWrapper2(IFactory2 factory2, MvxLayoutInflater inflater,
                MvxBinderPlaceholder factoryPlaceholder)
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
                return _factoryPlaceholder.OnViewCreated(
                    _inflater.CreateCustomViewInternal(
                        parent,
                        _factory2.OnCreateView(parent, name, context, attrs),
                        name, context, attrs),
                    context, attrs);
            }
        }
    }
}
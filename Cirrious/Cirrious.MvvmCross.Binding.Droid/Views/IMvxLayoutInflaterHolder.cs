// IMvxLayoutInflater.cs
// (c) Copyright Cirrious Ltd. http://www.cirrious.com
// MvvmCross is licensed using Microsoft Public License (Ms-PL)
// Contributions and inspirations noted in readme.md and license.txt
// 
// Project Lead - Stuart Lodge, @slodge, me@slodge.com

using Android.Content;
using Android.OS;
using Android.Util;
using Android.Views;
using Cirrious.CrossCore;
using Cirrious.MvvmCross.Binding.Droid.Binders;
using Cirrious.MvvmCross.Binding.Droid.BindingContext;
using Java.Interop;
using Java.Lang.Reflect;

namespace Cirrious.MvvmCross.Binding.Droid.Views
{
    public interface IMvxLayoutInflaterHolder
    {
        LayoutInflater LayoutInflater { get; }
    }

    /// <summary>
    /// Custom LayoutInflater responsible for inflating views and hooking up bindings
    /// Typically this is attached to MvxActivity and co via our MvxContextWrapper.
    /// 
    /// Potential order of view creation is the following (HC+):
    ///   1. IFactory2.OnCreateView
    ///   2. IFactory.OnCreateView
    ///   3. PrivateFactory.OnCreateView
    ///   4. OnCreateView(parent, name, attrs)
    ///   5. OnCreateView(name, attrs)
    ///   6. CreateView (sadly final)
    ///
    /// We intercept these calls and wrap any IFactory/IFactory2 with our own factory
    /// that binds when the view is returned.
    /// 
    /// Heavily based on Calligraphy's CalligraphyLayoutInflater
    /// See: https://github.com/chrisjenx/Calligraphy/blob/master/calligraphy/src/main/java/uk/co/chrisjenx/calligraphy/CalligraphyLayoutInflater.java" />
    /// </summary>
    public class MvxLayoutInflater : LayoutInflater
    {
        public class MvxBindingVisitor
        {
            public IMvxLayoutInflaterHolderFactory Factory { get; set; }

            public View OnViewCreated(View view, Context context, IAttributeSet attrs)
            {
                if (Factory != null && view != null && view.GetTag(Resource.Id.MvvmCrossTagId) != Java.Lang.Boolean.True)
                {
                    // Bind here.
                    view = Factory.BindView(view, context, attrs);

                    view.SetTag(Resource.Id.MvvmCrossTagId, Java.Lang.Boolean.True);
                }

                return view;
            }
        }

        internal static BuildVersionCodes Sdk = Build.VERSION.SdkInt;

        private readonly MvxBindingVisitor _bindingVisitor;

        private IMvxAndroidViewFactory _androidViewFactory;
        private IMvxLayoutInflaterHolderFactoryFactory _layoutInflaterHolderFactoryFactory;
        private Field _constructorArgs;
        private bool _setPrivateFactory;


        public MvxLayoutInflater(Context context)
            : base(context)
        {
            this._bindingVisitor = new MvxBindingVisitor();
            SetupLayoutFactories(false);
        }

        public MvxLayoutInflater(LayoutInflater original, Context newContext, MvxBindingVisitor bindingVisitor, bool cloned)
            : base(original, newContext)
        {
            this._bindingVisitor = bindingVisitor ?? new MvxBindingVisitor();

            SetupLayoutFactories(cloned);
        }

        public override LayoutInflater CloneInContext(Context newContext)
        {
            return new MvxLayoutInflater(this, newContext, _bindingVisitor, true);
        }

        // We can't call this.  See: https://bugzilla.xamarin.com/show_bug.cgi?id=30843
        //public override View Inflate(XmlReader parser, ViewGroup root, bool attachToRoot)
        //{
        //    SetPrivateFactoryInternal();
        //    return base.Inflate(parser, root, attachToRoot);
        //}

        // Calligraphy doesn't override this one...
        public override View Inflate(int resource, ViewGroup root, bool attachToRoot)
        {
            // Make sure our private factory is set since LayoutInflater > Honeycomb
            // uses a private factory.
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
                        this._bindingVisitor.Factory = factory;
                }

                // Inflate the resource
                var view = base.Inflate(resource, root, attachToRoot);


                // Register bindings with clear key
                if (currentBindingContext != null)
                {
                    if (factory != null)
                        currentBindingContext.RegisterBindingsWithClearKey(view, factory.CreatedBindings);
                }

                return view;
            }
            finally
            {
                this._bindingVisitor.Factory = null;
            }
        }

        protected override View OnCreateView(View parent, string name, IAttributeSet attrs)
        {
            return this._bindingVisitor.OnViewCreated(
                base.OnCreateView(parent, name, attrs), this.Context, attrs);
        }

        protected override View OnCreateView(string name, IAttributeSet attrs)
        {
            View view = AndroidViewFactory.CreateView(null, name, this.Context, attrs);
            if (view == null)
                view = base.OnCreateView(name, attrs);
            return this._bindingVisitor.OnViewCreated(view, view.Context, attrs);
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
                    new MvxLayoutInflaterCompat.FactoryWrapper(new DelegateFactory1(factory, this._bindingVisitor));
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
                    new MvxLayoutInflaterCompat.FactoryWrapper2(new DelegateFactory2(factory2, this._bindingVisitor));
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
                    MvxLayoutInflaterCompat.SetFactory(this, new DelegateFactory2(Factory2, this._bindingVisitor));
                }
            }

            if (Factory != null && !(Factory is MvxLayoutInflaterCompat.FactoryWrapper)) // Check for FactoryWrapper may be too loose
            {
                MvxLayoutInflaterCompat.SetFactory(this, new DelegateFactory1(Factory, this._bindingVisitor));
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

            Java.Lang.Class layoutInflaterClass = Java.Lang.Class.FromType(typeof(LayoutInflater));
            Method setPrivateFactoryMethod = layoutInflaterClass.GetMethod("setPrivateFactory", Java.Lang.Class.FromType(typeof(IFactory2)));
            if (setPrivateFactoryMethod != null)
            {
                try
                {
                    setPrivateFactoryMethod.Accessible = true;
                    setPrivateFactoryMethod.Invoke(this,
                        new PrivateFactoryWrapper2((IFactory2)Context, this, this._bindingVisitor));
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
                    Java.Lang.Class layoutInflaterClass = Java.Lang.Class.FromType(typeof(LayoutInflater));
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
                catch (Java.Lang.ClassNotFoundException ignored) {}
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

        private class DelegateFactory2 : IMvxLayoutInflaterFactory
        {
            private readonly IFactory2 _factory;
            private readonly MvxBindingVisitor _factoryPlaceholder;

            public DelegateFactory2(IFactory2 factoryToWrap, MvxBindingVisitor binder)
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
            private readonly MvxBindingVisitor _factoryPlaceholder;

            public DelegateFactory1(IFactory factoryToWrap, MvxBindingVisitor bindingVisitor)
            {
                _factory = factoryToWrap;
                _factoryPlaceholder = bindingVisitor;
            }

            public View OnCreateView(View parent, string name, Context context, IAttributeSet attrs)
            {
                return _factoryPlaceholder.OnViewCreated(
                    _factory.OnCreateView(name, context, attrs),
                    context, attrs);
            }
        }

        private class PrivateFactoryWrapper2 : Java.Lang.Object, IFactory2
        {
            private readonly IFactory2 _factory2;
            private readonly MvxBindingVisitor _bindingVisitor;
            private readonly MvxLayoutInflater _inflater;

            internal PrivateFactoryWrapper2(IFactory2 factory2, MvxLayoutInflater inflater,
                MvxBindingVisitor bindingVisitor)
            {
                _factory2 = factory2;
                _inflater = inflater;
                this._bindingVisitor = bindingVisitor;
            }

            public View OnCreateView(string name, Context context, IAttributeSet attrs)
            {
                return this._bindingVisitor.OnViewCreated(
                    _factory2.OnCreateView(name, context, attrs),
                    context, attrs);
            }

            public View OnCreateView(View parent, string name, Context context, IAttributeSet attrs)
            {
                return this._bindingVisitor.OnViewCreated(
                    _inflater.CreateCustomViewInternal(
                        parent,
                        _factory2.OnCreateView(parent, name, context, attrs),
                        name, context, attrs),
                    context, attrs);
            }
        }
    }
}
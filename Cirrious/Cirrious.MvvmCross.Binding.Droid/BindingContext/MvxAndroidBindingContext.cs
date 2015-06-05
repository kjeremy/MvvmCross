// MvxAndroidBindingContext.cs
// (c) Copyright Cirrious Ltd. http://www.cirrious.com
// MvvmCross is licensed using Microsoft Public License (Ms-PL)
// Contributions and inspirations noted in readme.md and license.txt
// 
// Project Lead - Stuart Lodge, @slodge, me@slodge.com

using System;
using Android.Content;
using Android.Views;
using Cirrious.CrossCore;
using Cirrious.MvvmCross.Binding.BindingContext;
using Cirrious.MvvmCross.Binding.Droid.Binders;
using Cirrious.MvvmCross.Binding.Droid.Views;

namespace Cirrious.MvvmCross.Binding.Droid.BindingContext
{
    public class MvxAndroidBindingContext
        : MvxBindingContext, IMvxAndroidBindingContext
    {
        private readonly Context _droidContext;
        private IMvxLayoutInflaterHolder _layoutInflaterHolder;
        private IMvxLayoutInflaterHolderFactoryFactory _layoutInflaterHolderFactoryFactory;

        public MvxAndroidBindingContext(Context droidContext, IMvxLayoutInflaterHolder layoutInflaterHolder, object source = null)
            : base(source)
        {
            _droidContext = droidContext;
            this._layoutInflaterHolder = layoutInflaterHolder;
        }

        public IMvxLayoutInflaterHolder LayoutInflaterHolder
        {
            get { return this._layoutInflaterHolder; }
            set { this._layoutInflaterHolder = value; }
        }

        internal protected IMvxLayoutInflaterHolderFactoryFactory FactoryFactory
        {
            get
            {
                if (this._layoutInflaterHolderFactoryFactory == null)
                    this._layoutInflaterHolderFactoryFactory = Mvx.Resolve<IMvxLayoutInflaterHolderFactoryFactory>();
                return this._layoutInflaterHolderFactoryFactory;
            }
        }

        public virtual View BindingInflate(int resourceId, ViewGroup viewGroup)
        {
            return BindingInflate(resourceId, viewGroup, true);
        }

        public virtual View BindingInflate(int resourceId, ViewGroup viewGroup, bool attachToRoot)
        {
            var view = CommonInflate(
                resourceId,
                viewGroup,
                FactoryFactory.Create(DataContext),
                attachToRoot);
            return view;
        }

        [Obsolete("Switch to new CommonInflate method - with additional attachToRoot parameter")]
        protected virtual View CommonInflate(int resourceId, ViewGroup viewGroup,
                                             IMvxLayoutInflaterHolderFactory factory)
        {
            return CommonInflate(resourceId, viewGroup, factory, viewGroup != null);
        }

        protected virtual View CommonInflate(int resourceId, ViewGroup viewGroup,
                                             IMvxLayoutInflaterHolderFactory factory, bool attachToRoot)
        {
            using (new MvxBindingContextStackRegistration<IMvxAndroidBindingContext>(this))
            {
                var layoutInflator = this._layoutInflaterHolder.LayoutInflater;
                //using (var clone = layoutInflator.CloneInContext(_droidContext))
                {
                    return layoutInflator.Inflate(resourceId, viewGroup, attachToRoot);
                }
            }
        }
    }
}
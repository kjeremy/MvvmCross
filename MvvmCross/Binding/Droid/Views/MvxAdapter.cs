// MvxAdapter.cs

// MvvmCross is licensed using Microsoft Public License (Ms-PL)
// Contributions and inspirations noted in readme.md and license.txt
//
// Project Lead - Stuart Lodge, @slodge, me@slodge.com

namespace MvvmCross.Binding.Droid.Views
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Collections.Specialized;

	using Android.Content;
	using Android.Runtime;
	using Android.Views;
	using Android.Widget;

	using MvvmCross.Binding.Attributes;
	using MvvmCross.Binding.Droid.BindingContext;
	using MvvmCross.Binding.ExtensionMethods;
	using MvvmCross.Platform;
	using MvvmCross.Platform.Exceptions;
	using MvvmCross.Platform.Platform;
	using MvvmCross.Platform.WeakSubscription;

	using Object = Java.Lang.Object;

	public class MvxAdapter
        : BaseAdapter
        , IMvxAdapter
    {
	    private int _itemTemplateId;
        private int _dropDownItemTemplateId;
        private IEnumerable _itemsSource;
        private IDisposable _subscription;

        // Note - _currentSimpleId is a bit of a hack
        //      - it's essentially a state flag identifying wheter we are currently inflating a dropdown or a normal view
        //      - ideally we would have passed the current simple id as a parameter through the GetView chain instead
        //      - but that was too big a breaking change for this release (7th Oct 2013)
        private int _currentSimpleId;

        // Note2 - _currentParent is similarly a bit of a hack
        //       - it is just here to avoid a breaking api change for now
        //       - will seek to remove both the _currentSimpleId and _currentParent private fields in a major release soon
        private ViewGroup _currentParent;

        public MvxAdapter(Context context)
            : this(context, MvxAndroidBindingContextHelpers.Current())
        {
        }

        public MvxAdapter(Context context, IMvxAndroidBindingContext bindingContext)
        {
            Context = context;
            BindingContext = bindingContext;
            if (BindingContext == null)
            {
                throw new MvxException(
                    "bindingContext is null during MvxAdapter creation - Adapter's should only be created when a specific binding context has been placed on the stack");
            }
            SimpleViewLayoutId = Android.Resource.Layout.SimpleListItem1;
            SimpleDropDownViewLayoutId = Android.Resource.Layout.SimpleSpinnerDropDownItem;
        }

        protected MvxAdapter(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }

        protected Context Context { get; }

        protected IMvxAndroidBindingContext BindingContext { get; }

        public int SimpleViewLayoutId { get; set; }

        public int SimpleDropDownViewLayoutId { get; set; }

        public bool ReloadOnAllItemsSourceSets { get; set; }

        [MvxSetToNullAfterBinding]
        public virtual IEnumerable ItemsSource
        {
            get { return _itemsSource; }
            set { SetItemsSource(value); }
        }

        public virtual int ItemTemplateId
        {
            get { return _itemTemplateId; }
            set
            {
                if (_itemTemplateId == value)
                    return;
                _itemTemplateId = value;

                // since the template has changed then let's force the list to redisplay by firing NotifyDataSetChanged()
                if (_itemsSource != null)
                    NotifyDataSetChanged();
            }
        }

        public virtual int DropDownItemTemplateId
        {
            get { return _dropDownItemTemplateId; }
            set
            {
                if (_dropDownItemTemplateId == value)
                    return;
                _dropDownItemTemplateId = value;

                // since the template has changed then let's force the list to redisplay by firing NotifyDataSetChanged()
                if (_itemsSource != null)
                    NotifyDataSetChanged();
            }
        }

        public override int Count => _itemsSource.Count();

        protected virtual void SetItemsSource(IEnumerable value)
        {
            if (ReferenceEquals(_itemsSource, value)
                && !ReloadOnAllItemsSourceSets)
                return;

            if (_subscription != null)
            {
                _subscription.Dispose();
                _subscription = null;
            }

            _itemsSource = value;

            if (_itemsSource != null && !(_itemsSource is IList))
                MvxBindingTrace.Trace(MvxTraceLevel.Warning,
                                      "You are currently binding to IEnumerable - this can be inefficient, especially for large collections. Binding to IList is more efficient.");

            var newObservable = _itemsSource as INotifyCollectionChanged;
            if (newObservable != null)
            {
                _subscription = newObservable.WeakSubscribe(OnItemsSourceCollectionChanged);
            }
            NotifyDataSetChanged();
        }

        protected virtual void OnItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            NotifyDataSetChanged(e);
        }

        public virtual void NotifyDataSetChanged(NotifyCollectionChangedEventArgs e)
        {
            RealNotifyDataSetChanged();
        }

        public override void NotifyDataSetChanged()
        {
            RealNotifyDataSetChanged();
        }

        protected virtual void RealNotifyDataSetChanged()
        {
            try
            {
                base.NotifyDataSetChanged();
            }
            catch (Exception exception)
            {
                Mvx.Warning(
                    "Exception masked during Adapter RealNotifyDataSetChanged {0}. Are you trying to update your collection from a background task? See http://goo.gl/0nW0L6",
                    exception.ToLongString());
            }
        }

        public virtual int GetPosition(object item)
        {
            return _itemsSource.GetPosition(item);
        }

        public virtual object GetRawItem(int position)
        {
            return _itemsSource.ElementAt(position);
        }

        public override Object GetItem(int position)
        {
            // we return null to Java here
            // we do **not**: return new MvxJavaContainer<object>(GetRawItem(position));
            // - see @JonPryor's answer in http://stackoverflow.com/questions/13842864/why-does-the-gref-go-too-high-when-i-put-a-mvxbindablespinner-in-a-mvxbindableli/13995199#comment19319057_13995199
            return null;
        }

        public override long GetItemId(int position)
        {
            return position;
        }

        public override View GetDropDownView(int position, View convertView, ViewGroup parent)
        {
            _currentSimpleId = SimpleDropDownViewLayoutId;
            _currentParent = parent;
            var toReturn = GetView(position, convertView, parent, DropDownItemTemplateId);
            _currentParent = null;
            return toReturn;
        }

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            _currentSimpleId = SimpleViewLayoutId;
            _currentParent = parent;
            var toReturn = GetView(position, convertView, parent, ItemTemplateId);
            _currentParent = null;
            return toReturn;
        }

        protected virtual View GetView(int position, View convertView, ViewGroup parent, int templateId)
        {
            if (_itemsSource == null)
            {
                MvxBindingTrace.Trace(MvxTraceLevel.Error, "GetView called when ItemsSource is null");
                return null;
            }

            var source = GetRawItem(position);

			return GetBindableView(convertView, source, parent, templateId);
        }

        protected virtual View GetSimpleView(View convertView, object dataContext)
        {
            if (convertView == null)
            {
                convertView = CreateSimpleView(dataContext);
            }
            else
            {
                BindSimpleView(convertView, dataContext);
            }

            return convertView;
        }

        protected virtual void BindSimpleView(View convertView, object dataContext)
        {
            var textView = convertView as TextView;
            if (textView != null)
            {
                textView.Text = (dataContext ?? string.Empty).ToString();
            }
        }

        protected virtual View CreateSimpleView(object dataContext)
        {
            // note - this could technically be a non-binding inflate - but the overhead is minimal
            // note - it's important to use `false` for the attachToRoot argument here
            //    see discussion in https://github.com/MvvmCross/MvvmCross/issues/507
            var view = BindingContext.BindingInflate(_currentSimpleId, _currentParent, false);
            BindSimpleView(view, dataContext);
            return view;
        }

        //protected virtual View GetBindableView(View convertView, object dataContext)
        //{
        //    return GetBindableView(convertView, dataContext, ItemTemplateId);
        //}

        protected virtual View GetBindableView(View convertView, object dataContext, ViewGroup parent, int templateId)
        {
            if (templateId == 0)
            {
                // no template seen - so use a standard string view from Android and use ToString()
                return GetSimpleView(convertView, dataContext);
            }

            // we have a templateid so lets use bind and inflate on it :)
            var viewToUse = convertView?.Tag as IMvxListItemView;
            if (viewToUse != null)
            {
                if (viewToUse.TemplateId != templateId)
                {
                    viewToUse = null;
                }
            }

            if (viewToUse == null)
            {
                viewToUse = CreateBindableView(dataContext, parent, templateId);
                viewToUse.Content.Tag = viewToUse as Java.Lang.Object;
            }
            else
            {
                BindBindableView(dataContext, viewToUse);
            }

            return viewToUse.Content;// as View;
        }

        protected virtual void BindBindableView(object source, IMvxListItemView viewToUse)
        {
            viewToUse.DataContext = source;
        }

        protected virtual IMvxListItemView CreateBindableView(object dataContext, ViewGroup parent, int templateId)
        {
            return new MvxListItemView(Context, BindingContext.LayoutInflaterHolder, dataContext, parent, templateId);
        }
    }

	public class MvxAdapter<TItem> : MvxAdapter where TItem : class
	{
		public MvxAdapter(Context context) : base(context, MvxAndroidBindingContextHelpers.Current())
		{
		}

		public MvxAdapter(Context context, IMvxAndroidBindingContext bindingContext) : base(context, bindingContext)
		{
		}

		public MvxAdapter(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
		{
		}

        [MvxSetToNullAfterBinding]
        public new IEnumerable<TItem> ItemsSource
		{
			get
			{
				return base.ItemsSource as IEnumerable<TItem>;
			}
			set
			{
				base.ItemsSource = value;
			}
		}
	} 
}

using Android.App;
using MvvmCross.Core.ViewModels;
using MvvmCross.Droid.Shared.Attributes;
using MvvmCross.Platform;
using MvvmCross.Platform.Droid.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MvvmCross.Droid.Shared.Presenter
{
    public class FragmentHostRegistrationSettings
    {
        private readonly IEnumerable<Assembly> _assembliesToLookup;
        private readonly IMvxViewModelTypeFinder _viewModelTypeFinder;

        private readonly Dictionary<Type, IList<MvxFragmentAttribute>> _fragmentTypeToMvxFragmentAttributeMap;
        private Dictionary<Type, Type> _viewModelToFragmentTypeMap;

        private bool isInitialized;

        public FragmentHostRegistrationSettings(IEnumerable<Assembly> assembliesToLookup)
        {
            _assembliesToLookup = assembliesToLookup;
            _viewModelTypeFinder = Mvx.Resolve<IMvxViewModelTypeFinder>();
            _fragmentTypeToMvxFragmentAttributeMap = new Dictionary<Type, IList<MvxFragmentAttribute>>();
        }

        private void InitializeIfNeeded()
        {
            lock (this)
            {
                if (isInitialized)
                    return;

                isInitialized = true;

                var pairOfTypeAndMvxFragmentAttributes =
                    _assembliesToLookup
                        .SelectMany(x => x.DefinedTypes)
                        .Select(x => x.AsType())
                        .Select(type =>
                        {
                            MvxFragmentAttribute[] attrs;
                            type.HasMvxFragmentAttribute(out attrs);
                            return new { type, attrs };
                        })
                        .Where(typeAndAttrs => typeAndAttrs.attrs?.Length > 0);

                foreach (var typeAttrPair in pairOfTypeAndMvxFragmentAttributes)
                {
                    var type = typeAttrPair.type;
                    var attrs = typeAttrPair.attrs;

                    IList<MvxFragmentAttribute> attributeList;
                    if (!_fragmentTypeToMvxFragmentAttributeMap.TryGetValue(type, out attributeList))
                    {
                        attributeList = new List<MvxFragmentAttribute>();
                        _fragmentTypeToMvxFragmentAttributeMap.Add(type, attributeList);
                    }

                    foreach (var mvxAttribute in attrs)
                        attributeList.Add(mvxAttribute);
                }

                _viewModelToFragmentTypeMap =
                    pairOfTypeAndMvxFragmentAttributes.ToDictionary(p => GetAssociatedViewModelType(p.type, p.attrs), p => p.type);
            }
        }

        private Type GetAssociatedViewModelType(Type fromFragmentType, IEnumerable<MvxFragmentAttribute> fragmentAttributes)
        {
            Type viewModelType = _viewModelTypeFinder.FindTypeOrNull(fromFragmentType);

            return viewModelType ?? fragmentAttributes.First().ViewModelType;
        }

        public bool IsTypeRegisteredAsFragment(Type viewModelType)
        {
            InitializeIfNeeded();

            return _viewModelToFragmentTypeMap.ContainsKey(viewModelType);
        }

        public bool IsActualHostValid(Type forViewModelType)
        {
            InitializeIfNeeded();

            var activityViewModelType = GetCurrentActivityViewModelType();

            // for example: MvxSplashScreen usually does not have associated ViewModel
            // it is for sure not valid host - and it can not be used with Fragment Presenter.
            if (activityViewModelType == typeof(MvxNullViewModel))
                return false;

            return
                GetMvxFragmentAssociatedAttributes(forViewModelType)
                    .Any(x => x.ParentActivityViewModelType == activityViewModelType);
        }

        private Type GetCurrentActivityViewModelType()
        {
            Activity currentActivity = Mvx.Resolve<IMvxAndroidCurrentTopActivity>().Activity;
            Type currentActivityType = currentActivity.GetType();

            var activityViewModelType = _viewModelTypeFinder.FindTypeOrNull(currentActivityType);
            return activityViewModelType;
        }

        public Type GetFragmentHostViewModelType(Type forViewModelType)
        {
            InitializeIfNeeded();

            var associatedMvxFragmentAttributes = GetMvxFragmentAssociatedAttributes(forViewModelType).ToList();
            return associatedMvxFragmentAttributes.First().ParentActivityViewModelType;
        }

        public Type GetFragmentTypeAssociatedWith(Type viewModelType)
        {
            InitializeIfNeeded();

            return _viewModelToFragmentTypeMap[viewModelType];
        }

        private IList<MvxFragmentAttribute> GetMvxFragmentAssociatedAttributes(Type withFragmentViewModelType)
        {
            var fragmentTypeAssociatedWithViewModel = GetFragmentTypeAssociatedWith(withFragmentViewModelType);
            return _fragmentTypeToMvxFragmentAttributeMap[fragmentTypeAssociatedWithViewModel];
        }

        public MvxFragmentAttribute GetMvxFragmentAttributeAssociatedWithCurrentHost(Type fragmentViewModelType)
        {
            InitializeIfNeeded();

            var currentActivityViewModelType = GetCurrentActivityViewModelType();

            return
                GetMvxFragmentAssociatedAttributes(fragmentViewModelType)
                    .Single(x => x.ParentActivityViewModelType == currentActivityViewModelType);
        }
    }
}
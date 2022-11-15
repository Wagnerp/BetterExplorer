﻿//Copyright (c) Microsoft Corporation.  All rights reserved.

using BExplorer.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using WPFUI.Win32;

namespace BExplorer.Shell
{
    /// <summary>
    /// Exposes properties and methods for retrieving information about a search condition.
    /// </summary>
    public class SearchCondition : IDisposable
    {
        private PROPERTYKEY propertyKey;
        private PROPERTYKEY emptyPropertyKey = new PROPERTYKEY();

        private string canonicalName;
        /// <summary>The name of a property to be compared or NULL for an unspecified property.</summary>
        public string PropertyCanonicalName => canonicalName;

        /// <summary>
        /// A value (in <see cref="System.String"/> format) to which the property is compared. 
        /// </summary>
        public string PropertyValue { get; internal set; }
        internal ICondition NativeSearchCondition { get; set; }

        private SearchConditionOperation conditionOperation = SearchConditionOperation.Implicit;

        /// <summary>
        /// Search condition operation to be performed on the property/value combination.
        /// See <see cref="SearchConditionOperation"/> for more details.
        /// </summary>        
        public SearchConditionOperation ConditionOperation => conditionOperation;


        private SearchConditionType conditionType = SearchConditionType.Leaf;
        /// <summary>
        /// Represents the condition type for the given node. 
        /// </summary>        
        public SearchConditionType ConditionType => conditionType;



        /// <summary>
        /// The property key for the property that is to be compared.
        /// </summary>        
        public PROPERTYKEY PropertyKey
        {
            get
            {
                if (propertyKey.fmtid == emptyPropertyKey.fmtid && propertyKey.pid == emptyPropertyKey.pid)
                {
                    PropertySystemNativeMethods.PSGetPropertyKeyFromName(PropertyCanonicalName, out propertyKey);
                }

                return propertyKey;
            }
        }

        internal SearchCondition(ICondition nativeSearchCondition)
        {
            if (nativeSearchCondition == null) throw new ArgumentNullException("nativeSearchCondition");

            NativeSearchCondition = nativeSearchCondition;

            HResult hr = NativeSearchCondition.GetConditionType(out conditionType);

            if (hr != HResult.S_OK) return;

            if (ConditionType == SearchConditionType.Leaf)
            {
                using (var propVar = new PropVariant())
                {
                    hr = NativeSearchCondition.GetComparisonInfo(out canonicalName, out conditionOperation, propVar);

                    if (hr != HResult.S_OK) return;

                    PropertyValue = propVar.Value.ToString();
                }
            }
        }

        /// <summary>
        /// Retrieves an array of the sub-conditions. 
        /// </summary>
        public IEnumerable<SearchCondition> GetSubConditions()
        {
            // Our list that we'll return
            var subConditionsList = new List<SearchCondition>();

            // Get the sub-conditions from the native API
            object subConditionObj;
            var guid = new Guid(InterfaceGuids.IEnumUnknown);

            HResult hr = NativeSearchCondition.GetSubConditions(ref guid, out subConditionObj);

            if (hr != HResult.S_OK) throw new Exception(hr.ToString());

            // Convert each ICondition to SearchCondition
            if (subConditionObj != null)
            {
                var enumUnknown = subConditionObj as IEnumUnknown;

                IntPtr buffer = IntPtr.Zero;
                uint fetched = 0;

                while (hr == HResult.S_OK)
                {
                    hr = enumUnknown.Next(1, ref buffer, ref fetched);

                    if (hr == HResult.S_OK && fetched == 1)
                    {
                        subConditionsList.Add(new SearchCondition((ICondition)Marshal.GetObjectForIUnknown(buffer)));
                    }
                }
            }

            return subConditionsList;
        }

        #region IDisposable Members

        /// <summary>
        /// 
        /// </summary>
        ~SearchCondition() { Dispose(false); }


        /// <summary>
        /// Release the native objects.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Release the native objects.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (NativeSearchCondition != null)
            {
                Marshal.ReleaseComObject(NativeSearchCondition);
                NativeSearchCondition = null;
            }
        }

        #endregion

    }
}

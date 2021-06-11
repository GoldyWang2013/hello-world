#region Copyright (c) 2000-2021 Developer Express Inc.
/*
{*******************************************************************}
{                                                                   }
{       Developer Express .NET Component Library                    }
{                                                                   }
{                                                                   }
{       Copyright (c) 2000-2021 Developer Express Inc.              }
{       ALL RIGHTS RESERVED                                         }
{                                                                   }
{   The entire contents of this file is protected by U.S. and       }
{   International Copyright Laws. Unauthorized reproduction,        }
{   reverse-engineering, and distribution of all or any portion of  }
{   the code contained in this file is strictly prohibited and may  }
{   result in severe civil and criminal penalties and will be       }
{   prosecuted to the maximum extent possible under the law.        }
{                                                                   }
{   RESTRICTIONS                                                    }
{                                                                   }
{   THIS SOURCE CODE AND ALL RESULTING INTERMEDIATE FILES           }
{   ARE CONFIDENTIAL AND PROPRIETARY TRADE                          }
{   SECRETS OF DEVELOPER EXPRESS INC. THE REGISTERED DEVELOPER IS   }
{   LICENSED TO DISTRIBUTE THE PRODUCT AND ALL ACCOMPANYING .NET    }
{   CONTROLS AS PART OF AN EXECUTABLE PROGRAM ONLY.                 }
{                                                                   }
{   THE SOURCE CODE CONTAINED WITHIN THIS FILE AND ALL RELATED      }
{   FILES OR ANY PORTION OF ITS CONTENTS SHALL AT NO TIME BE        }
{   COPIED, TRANSFERRED, SOLD, DISTRIBUTED, OR OTHERWISE MADE       }
{   AVAILABLE TO OTHER INDIVIDUALS WITHOUT EXPRESS WRITTEN CONSENT  }
{   AND PERMISSION FROM DEVELOPER EXPRESS INC.                      }
{                                                                   }
{   CONSULT THE END USER LICENSE AGREEMENT FOR INFORMATION ON       }
{   ADDITIONAL RESTRICTIONS.                                        }
{                                                                   }
{*******************************************************************}
*/
#endregion Copyright (c) 2000-2021 Developer Express Inc.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using DevExpress.Mvvm.Native;
using System.Runtime.CompilerServices;


namespace Fx.Common.Entity {
	public partial class CEntity {
		//---------------------------------------------------------------------
		// This method is called by the Set accessor of each property.  
		// The CallerMemberName attribute that is applied to the optional propertyName  
		// parameter causes the property name of the caller to be substituted as an argument.  
		protected void NotifyPropertyChanged([CallerMemberName] String propertyName = "") {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
		//---------------------------------------------------------------------
		protected virtual void OnPropertyChanged(string propertyName) {
			if(PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
		protected virtual void OnPropertyChanged(PropertyChangedEventArgs e) {
			if(PropertyChanged != null)
				PropertyChanged(this, e);
		}
		//---------------------------------------------------------------------
		public static string GetPropertyName<T>(Expression<Func<T>> expression) {
			/// ???
			return GetPropertyNameFast(expression);
		}
		//---------------------------------------------------------------------
		internal static string GetPropertyNameFast(LambdaExpression expression) {
			MemberExpression memberExpression = expression.Body as MemberExpression;
			if(memberExpression == null) {
				throw new ArgumentException("MemberExpression is expected in expression.Body", "expression");
			}
			const string vblocalPrefix = "$VB$Local_";
			var member = memberExpression.Member;
			if(
#if !NETFX_CORE
				member.MemberType == System.Reflection.MemberTypes.Field &&
#endif
				member.Name != null &&
				member.Name.StartsWith(vblocalPrefix))
				return member.Name.Substring(vblocalPrefix.Length);
			return member.Name;
		}
		public event PropertyChangedEventHandler PropertyChanged;
		public T GetProperty<T>(Expression<Func<T>> expression) {
			return GetPropertyCore<T>(GetPropertyName(expression));
		}
		//---------------------------------------------------
		public string GetPropertyNameTest() {
			var idName = GetProperty(() => Id);
			// Good
			//var bbb = GetProperty(() => BookmarkName );
			return null;
		}
		//---------------------------------------------------
		protected virtual bool SetProperty<T>(ref T storage, T value, string propertyName, Action changedCallback) {
			VerifyAccess();
			if(CompareValues<T>(storage, value))
				return false;
			storage = value;
			RaisePropertyChanged(propertyName);
			changedCallback?.Invoke();
			return true;
		}
		protected bool SetProperty<T>(ref T storage, T value, Expression<Func<T>> expression, Action changedCallback) {
			return SetProperty(ref storage, value, GetPropertyName(expression), changedCallback);
		}
		protected bool SetProperty<T>(ref T storage, T value, Expression<Func<T>> expression) {
			return SetProperty<T>(ref storage, value, expression, null);
		}
#if !NETFX_CORE
		protected bool SetProperty<T>(ref T storage, T value, string propertyName) {
#else
		protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null) {
#endif
			return this.SetProperty<T>(ref storage, value, propertyName, null);
		}
#if NETFX_CORE
		protected T GetProperty<T>([CallerMemberName] string propertyName = null) {
			return GetPropertyCore<T>(propertyName);
		}
		protected bool SetProperty<T>(ref T storage, T value, Action<T, T> changedCallback, [CallerMemberName] string propertyName = null) {
			if(object.Equals(storage, value)) return false;
			T oldValue = storage;
			storage = value;
			if(changedCallback != null)
				changedCallback(oldValue, value);
			RaisePropertiesChanged(propertyName);
			return true;
		}
		protected bool SetProperty<T>(T value, Action changedCallback, [CallerMemberName] string propertyName = null) {
			return SetPropertyCore(propertyName, value, changedCallback);
		}
		protected bool SetProperty<T>(T value, Action<T> changedCallback, [CallerMemberName] string propertyName = null) {
			return SetPropertyCore(propertyName, value, changedCallback);
		}
		protected bool SetProperty<T>(T value, [CallerMemberName] string propertyName = null) {
			return SetPropertyCore(propertyName, value, (Action)null);
		}
#else
		protected T GetValue<T>([CallerMemberName] string propertyName = null) {
			GuardPropertyName(propertyName);
			return GetPropertyCore<T>(propertyName);
		}
		protected bool SetValue<T>(T value, [CallerMemberName] string propertyName = null) {
			return SetValue(value, default(Action), propertyName);
		}
		protected bool SetValue<T>(T value, Action changedCallback, [CallerMemberName] string propertyName = null) {
			return SetPropertyCore(propertyName, value, changedCallback);
		}
		protected bool SetValue<T>(T value, Action<T> changedCallback, [CallerMemberName] string propertyName = null) {
			return SetPropertyCore(propertyName, value, changedCallback);
		}
		protected bool SetValue<T>(ref T storage, T value, [CallerMemberName] string propertyName = null) {
			return SetValue(ref storage, value, default(Action), propertyName);
		}
		protected bool SetValue<T>(ref T storage, T value, Action changedCallback, [CallerMemberName] string propertyName = null) {
			GuardPropertyName(propertyName);
			return SetProperty(ref storage, value, propertyName, changedCallback);
		}
		static void GuardPropertyName(string propertyName) {
			if(string.IsNullOrEmpty(propertyName))
				throw new ArgumentNullException(nameof(propertyName));
		}
#endif
		protected bool SetProperty<T>(Expression<Func<T>> expression, T value) {
			return SetProperty(expression, value, (Action)null);
		}
		protected bool SetProperty<T>(Expression<Func<T>> expression, T value, Action changedCallback) {
			string propertyName = GetPropertyName(expression);
			return SetPropertyCore(propertyName, value, changedCallback);
		}
		protected bool SetProperty<T>(Expression<Func<T>> expression, T value, Action<T> changedCallback) {
			string propertyName = GetPropertyName(expression);
			return SetPropertyCore(propertyName, value, changedCallback);
		}
		#region RaisePropertyChanged
#if !NETFX_CORE
		protected void RaisePropertyChanged(string propertyName) {
#else
		protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
#endif
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
#if !NETFX_CORE
		protected void RaisePropertyChanged() {
			RaisePropertiesChanged(null);
		}
#endif
		protected void RaisePropertyChanged<T>(Expression<Func<T>> expression) {
			RaisePropertyChanged(GetPropertyName(expression));
		}
		protected void RaisePropertiesChanged(params string[] propertyNames) {
			if(propertyNames == null || propertyNames.Length == 0) {
				RaisePropertyChanged(string.Empty);
				return;
			}
			foreach(string propertyName in propertyNames) {
				RaisePropertyChanged(propertyName);
			}
		}
		protected void RaisePropertiesChanged<T1, T2>(Expression<Func<T1>> expression1, Expression<Func<T2>> expression2) {
			RaisePropertyChanged(expression1);
			RaisePropertyChanged(expression2);
		}
		protected void RaisePropertiesChanged<T1, T2, T3>(Expression<Func<T1>> expression1, Expression<Func<T2>> expression2, Expression<Func<T3>> expression3) {
			RaisePropertyChanged(expression1);
			RaisePropertyChanged(expression2);
			RaisePropertyChanged(expression3);
		}
		protected void RaisePropertiesChanged<T1, T2, T3, T4>(Expression<Func<T1>> expression1, Expression<Func<T2>> expression2, Expression<Func<T3>> expression3, Expression<Func<T4>> expression4) {
			RaisePropertyChanged(expression1);
			RaisePropertyChanged(expression2);
			RaisePropertyChanged(expression3);
			RaisePropertyChanged(expression4);
		}
		protected void RaisePropertiesChanged<T1, T2, T3, T4, T5>(Expression<Func<T1>> expression1, Expression<Func<T2>> expression2, Expression<Func<T3>> expression3, Expression<Func<T4>> expression4, Expression<Func<T5>> expression5) {
			RaisePropertyChanged(expression1);
			RaisePropertyChanged(expression2);
			RaisePropertyChanged(expression3);
			RaisePropertyChanged(expression4);
			RaisePropertyChanged(expression5);
		}
		#endregion
		#region property bag
		Dictionary<string, object> _propertyBag;
		Dictionary<string, object> PropertyBag => _propertyBag ?? (_propertyBag = new Dictionary<string, object>());
#if DEBUG
		internal Dictionary<string, object> PropertyBagForTests => PropertyBag;
#endif
		T GetPropertyCore<T>(string propertyName) {
			object val;
			if(PropertyBag.TryGetValue(propertyName, out val))
				return (T)val;
			return default(T);
		}
		bool SetPropertyCore<T>(string propertyName, T value, Action changedCallback) {
			T oldValue;
			var res = SetPropertyCore(propertyName, value, out oldValue);
			if(res) {
				changedCallback?.Invoke();
			}
			return res;
		}
		bool SetPropertyCore<T>(string propertyName, T value, Action<T> changedCallback) {
			T oldValue;
			var res = SetPropertyCore(propertyName, value, out oldValue);
			if(res) {
				changedCallback?.Invoke(oldValue);
			}
			return res;
		}
		protected virtual bool SetPropertyCore<T>(string propertyName, T value, out T oldValue) {
			VerifyAccess();
			oldValue = default(T);
			object val;
			if(PropertyBag.TryGetValue(propertyName, out val))
				oldValue = (T)val;
			if(CompareValues<T>(oldValue, value))
				return false;
			lock(PropertyBag) {
				PropertyBag[propertyName] = value;
			}
			RaisePropertyChanged(propertyName);
			return true;
		}
		protected virtual void VerifyAccess() {
		}
		static bool CompareValues<T>(T storage, T value) {
			return EqualityComparer<T>.Default.Equals(storage, value);
		}
		#endregion
	}
}

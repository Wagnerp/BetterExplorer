﻿// ******************************************************************
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THE CODE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
// THE CODE OR THE USE OR OTHER DEALINGS IN THE CODE.
// ******************************************************************
using System.Collections.Generic;
using Windows.UI.Notifications;

namespace BetterExplorer.Api {

  /// <summary>
  /// Manages the toast notifications for an app including the ability the clear all toast history and removing individual toasts.
  /// </summary>
  public sealed class DesktopNotificationHistoryCompat {
    private string _aumid;
    private ToastNotificationHistory _history;
    /// <summary>
    /// Do not call this. Instead, call <see cref="DesktopNotificationManagerCompat.History"/> to obtain an instance.
    /// </summary>
    /// <param name="aumid"></param>
    internal DesktopNotificationHistoryCompat(string aumid) {
      this._aumid = aumid;
      this._history = ToastNotificationManager.History;
    }
    /// <summary>
    /// Removes all notifications sent by this app from action center.
    /// </summary>
    public void Clear() {
      if (this._aumid != null) {
        this._history.Clear(this._aumid);
      } else {
        this._history.Clear();
      }
    }
    /// <summary>
    /// Gets all notifications sent by this app that are currently still in Action Center.
    /// </summary>
    /// <returns>A collection of toasts.</returns>
    public IReadOnlyList<ToastNotification> GetHistory() {
      return this._aumid != null ? this._history.GetHistory(this._aumid) : this._history.GetHistory();
    }
    /// <summary>
    /// Removes an individual toast, with the specified tag label, from action center.
    /// </summary>
    /// <param name="tag">The tag label of the toast notification to be removed.</param>
    public void Remove(string tag) {
      if (this._aumid != null) {
        this._history.Remove(tag, string.Empty, this._aumid);
      } else {
        this._history.Remove(tag);
      }
    }
    /// <summary>
    /// Removes a toast notification from the action using the notification's tag and group labels.
    /// </summary>
    /// <param name="tag">The tag label of the toast notification to be removed.</param>
    /// <param name="group">The group label of the toast notification to be removed.</param>
    public void Remove(string tag, string group) {
      if (this._aumid != null) {
        this._history.Remove(tag, group, this._aumid);
      } else {
        this._history.Remove(tag, group);
      }
    }
    /// <summary>
    /// Removes a group of toast notifications, identified by the specified group label, from action center.
    /// </summary>
    /// <param name="group">The group label of the toast notifications to be removed.</param>
    public void RemoveGroup(string group) {
      if (this._aumid != null) {
        this._history.RemoveGroup(group, this._aumid);
      } else {
        this._history.RemoveGroup(group);
      }
    }
  }

}
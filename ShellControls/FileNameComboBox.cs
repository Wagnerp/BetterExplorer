// BExplorer.Shell - A Windows Shell library for .Net.
// Copyright (C) 2007-2009 Steven J. Kirk
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either 
// version 2 of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public 
// License along with this program; if not, write to the Free 
// Software Foundation, Inc., 51 Franklin Street, Fifth Floor,  
// Boston, MA 2110-1301, USA.
//

using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using BExplorer.Shell;
using ShellControls.ShellListView;

namespace ShellControls {
  /// <summary>
  /// A filename combo box suitable for use in file Open/Save dialogs.
  /// </summary>
  /// 
  /// <remarks>
  /// <para>
  /// This control extends the <see cref="ComboBox"/> class to provide
  /// auto-completion of filenames based on the folder selected in a
  /// <see cref="ShellView"/>. The control also automatically navigates 
  /// the ShellView control when the user types a folder path.
  /// </para>
  /// </remarks>
  public class FileNameComboBox : ComboBox {
    bool m_TryAutoComplete;


    /// <summary>
    /// Gets/sets the <see cref="ShellView"/> control that the 
    /// <see cref="FileNameComboBox"/> should look for auto-completion
    /// hints.
    /// </summary>
    [Category("Behaviour")]
    [DefaultValue(null)]
    public ShellView ShellView { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileNameComboBox"/>
    /// class.
    /// </summary>
    [Category("Behaviour")]
    [DefaultValue(null)]
    public FileFilterComboBox FilterControl { get; set; }

    /// <summary>
    /// Occurs when a file name is entered into the 
    /// <see cref="FileNameComboBox"/> and the Return key pressed.
    /// </summary>
    public event EventHandler FileNameEntered;

    /// <summary>
    /// Determines whether the specified key is a regular input key or a 
    /// special key that requires preprocessing. 
    /// </summary>
    /// 
    /// <param name="keyData">
    /// One of the <see cref="Keys"/> values.
    /// </param>
    /// 
    /// <returns>
    /// true if the specified key is a regular input key; otherwise, false. 
    /// </returns>
    protected override bool IsInputKey(Keys keyData) => keyData == Keys.Enter ? true : base.IsInputKey(keyData);


    /// <summary>
    /// Raises the <see cref="Control.KeyDown"/> event.
    /// </summary>
    /// 
    /// <param name="e">
    /// A <see cref="KeyEventArgs"/> that contains the event data.
    /// </param>
    protected override void OnKeyDown(KeyEventArgs e) {
      base.OnKeyDown(e);
      if (e.KeyCode == Keys.Enter && Text.Length > 0 && !Open(Text) && FilterControl != null) FilterControl.Filter = Text;
      m_TryAutoComplete = false;
    }

    /// <summary>
    /// Raises the <see cref="Control.KeyPress"/> event.
    /// </summary>
    /// 
    /// <param name="e">
    /// A <see cref="KeyPressEventArgs"/> that contains the event data.
    /// </param>
    protected override void OnKeyPress(KeyPressEventArgs e) {
      base.OnKeyPress(e);
      m_TryAutoComplete = char.IsLetterOrDigit(e.KeyChar);
    }

    /// <summary>
    /// Raises the <see cref="Control.TextChanged"/> event.
    /// </summary>
    /// 
    /// <param name="e">
    /// An <see cref="EventArgs"/> that contains the event data.
    /// </param>
    protected override void OnTextChanged(EventArgs e) {
      base.OnTextChanged(e);
      if (m_TryAutoComplete) {
        try {
          AutoComplete();
        } catch (Exception) { }
      }
    }

    private void AutoComplete() {
      string path;
      string pattern;
      string[] matches;
      bool rooted = true;

      if (Text == string.Empty || Text.IndexOfAny(new char[] { '?', '*' }) != -1) return;

      path = Path.GetDirectoryName(Text);
      pattern = Path.GetFileName(Text);

      if (path == null || path == string.Empty && ShellView != null && ShellView.CurrentFolder.IsFileSystem && ShellView.CurrentFolder.ParsingName != ShellItem.Desktop.ParsingName) {
        path = ShellView.CurrentFolder.FileSystemPath;
        pattern = Text;
        rooted = false;
      }

      matches = Directory.GetFiles(path, pattern + '*');

      for (int n = 0; n < 2; ++n) {
        if (matches.Length > 0) {
          int currentLength = Text.Length;
          Text = rooted ? matches[0] : Path.GetFileName(matches[0]);
          SelectionStart = currentLength;
          SelectionLength = Text.Length;
          break;
        } else {
          matches = Directory.GetDirectories(path, pattern + '*');
        }
      }
    }

    private bool Open(string path) {
      //TODO: Find out if we can replace [result] with a returns
      bool result = false;

      if (File.Exists(path)) {
        FileNameEntered?.Invoke(this, EventArgs.Empty);
        result = true;
      } else if (Directory.Exists(path)) {
        if (ShellView != null) {
          Text = string.Empty;
          result = true;
        }
      } else {
        Text = Path.GetFileName(path);
      }

      if (!Path.IsPathRooted(path) && ShellView.CurrentFolder.IsFileSystem) result = Open(Path.Combine(ShellView.CurrentFolder.FileSystemPath, path));

      return result;
    }
  }
}

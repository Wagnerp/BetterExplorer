﻿using System;

namespace WPFUI.Win32
{
	/// <summary>
	/// Class containing definition for columns
	/// </summary>
	[Serializable]
	public class Collumns
	{
		public String ID { get; set; }
		public PROPERTYKEY pkey { get; set; }
		public string Name { get; set; }
		public int Width { get; set; }
		public Boolean IsColumnHandler { get; set; }
		public int Index { get; set; }

		public Type CollumnType { get; set; }
		public int MinWidth { get; set; }
	}
}

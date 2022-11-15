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

#pragma warning disable 1591

namespace BExplorer.Shell.Interop
{
    public class BHID
    {
        public static Guid SFObject { get; } = new Guid("3981e224-f559-11d3-8e3a-00c04f6837d5");
        public static Guid SFUIObject { get; } = new Guid("3981e225-f559-11d3-8e3a-00c04f6837d5");
        public static Guid SFAssocHandlers { get; } = new Guid("b8ab0b9c-c2ec-4f7a-918d-314900e6280a");
    }
}

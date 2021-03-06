﻿using System;
using System.Runtime.InteropServices;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;

namespace VVVV.Hosting.Pins.Input
{
    [ComVisible(false)]
	public class EnumInputPin<T> : DiffPin<T>
	{
		protected IEnumIn FEnumInputPin;
		protected Type FEnumType;
		
		public EnumInputPin(IPluginHost host, InputAttribute attribute)
			: base(host, attribute)
		{
			FEnumType = typeof(T);
			
			var entrys = Enum.GetNames(FEnumType);
			var defEntry = !string.IsNullOrEmpty(attribute.DefaultEnumEntry) ? attribute.DefaultEnumEntry : entrys[0];
			host.UpdateEnum(FEnumType.FullName, defEntry, entrys);
			
			host.CreateEnumInput(attribute.Name, (TSliceMode)attribute.SliceMode, (TPinVisibility)attribute.Visibility, out FEnumInputPin);
			FEnumInputPin.SetSubType(FEnumType.FullName);
			
			base.InitializeInternalPin(FEnumInputPin);
		}

		protected override bool IsInternalPinChanged
		{
			get
			{
				return FEnumInputPin.PinIsChanged;
			}
		}
		
		unsafe protected override void DoUpdate()
		{
			SliceCount = FEnumInputPin.SliceCount;
			
			for (int i = 0; i < FSliceCount; i++)
			{
				string entry;
				FEnumInputPin.GetString(i, out entry);
                try
                {
                    FBuffer[i] = (T)Enum.Parse(FEnumType, entry);
                }
                catch (Exception)
                {
                    FBuffer[i] = default(T);
                }
			}
		}
	}
}

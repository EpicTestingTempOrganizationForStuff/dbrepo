﻿using System;
using System.Collections.Generic;
using System.Text;

namespace DbRepo.Tests
{
	public static class TimespanExtensions
	{
		public static string ToHumanReadableString(this TimeSpan t)
		{
			if (t.TotalSeconds <= 1)
			{
				return $@"{t:s\.ff} seconds";
			}
			if (t.TotalMinutes <= 1)
			{
				return $@"{t:%s} seconds";
			}
			if (t.TotalHours <= 1)
			{
				return $@"{t:%m} minutes";
			}
			if (t.TotalDays <= 1)
			{
				return $@"{t:%h} hours";
			}

			return $@"{t:%d} days";
		}
	}
}

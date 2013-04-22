using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TuneOut.UnitTests
{
	[TestClass]
	public class CompareToTest
	{
		[TestMethod]
		public void CompareToTest1()
		{
			Random r = new Random();
			string[] s = new string[] { "QWERTY", "UIOP", "ASDFGHJKL", "zxcvbnm,./", "1234567&*()" };

			List<TestClass1> list = new List<TestClass1>();

			DateTime start = DateTime.UtcNow;
			for (int i = 0; i < 100000; i++)
			{
				TestClass1 t = new TestClass1(r.Next(), r.Next(), s[r.Next() % 5]);
				int insertionIndex = list.BinarySearch(t);
				if (insertionIndex < 0)
				{
					insertionIndex = ~insertionIndex;
				}

				list.Add(t);
			}
			DateTime end = DateTime.UtcNow;

			TimeSpan duration = end - start;
			Debug.WriteLine(duration);
		}

		[TestMethod]
		public void CompareToTest2()
		{
			Random r = new Random();
			string[] s = new string[] { "QWERTY", "UIOP", "ASDFGHJKL", "zxcvbnm,./", "1234567&*()" };

			List<TestClass2> list = new List<TestClass2>();

			DateTime start = DateTime.UtcNow;
			for (int i = 0; i < 100000; i++)
			{
				TestClass2 t = new TestClass2(r.Next(), r.Next(), s[r.Next() % 5]);
				int insertionIndex = list.BinarySearch(t);
				if (insertionIndex < 0)
				{
					insertionIndex = ~insertionIndex;
				}

				list.Add(t);
			}
			DateTime end = DateTime.UtcNow;

			TimeSpan duration = end - start;
			Debug.WriteLine(duration);
		}

		private class TestClass1 : IComparable<TestClass1>
		{
			private int Prop1 { get; set; }

			private int Prop2 { get; set; }

			private string Prop3 { get; set; }

			public TestClass1(int p1, int p2, string p3)
			{
				Prop1 = p1;
				Prop2 = p2;
				Prop3 = p3;
			}

			public int CompareTo(TestClass1 other)
			{
				var p1Diff = this.Prop1 - other.Prop1;
				if (p1Diff != 0)
					return p1Diff;

				var p2Diff = this.Prop2 - other.Prop2;
				if (p2Diff != 0)
					return p2Diff;

				return this.Prop3.CompareTo(other.Prop3);
			}
		}

		private class TestClass2 : IComparable<TestClass2>
		{
			private int Prop1 { get; set; }

			private int Prop2 { get; set; }

			private string Prop3 { get; set; }

			public TestClass2(int p1, int p2, string p3)
			{
				Prop1 = p1;
				Prop2 = p2;
				Prop3 = p3;
			}

			public int CompareTo(TestClass2 other)
			{
				return (this.Prop1 - other.Prop2) * 100 + (this.Prop2 - other.Prop2) + Math.Sign(this.Prop3.CompareTo(other.Prop3));
			}
		}
	}
}
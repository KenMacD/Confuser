//
// InstructionCollection.cs
//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Generated by /CodeGen/cecil-gen.rb do not edit
// Thu Sep 28 17:54:43 CEST 2006
//
// (C) 2005 Jb Evain
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

namespace Mono.Cecil.Cil {

	using System;
	using System.Collections;

	using Mono.Cecil.Cil;

	public sealed class InstructionCollection : CollectionBase, ICodeVisitable {

		MethodBody m_container;
		public readonly Instruction Outside = new Instruction (int.MaxValue, OpCodes.Nop);

		public Instruction this [int index] {
			get { return List [index] as Instruction; }
			set { List [index] = value; }
		}

		public MethodBody Container {
			get { return m_container; }
		}

		public InstructionCollection (MethodBody container)
		{
			m_container = container;
		}

		internal void Add (Instruction value)
		{
			List.Add (value);
            RecalculateOffsets();//Hack
        }

        internal void AddInternal(Instruction value)
        {
            List.Add(value);
        }

		public bool Contains (Instruction value)
		{
			return List.Contains (value);
		}

		public int IndexOf (Instruction value)
		{
			return List.IndexOf (value);
		}

		internal void Insert (int index, Instruction value)
		{
            List.Insert(index, value);
            RecalculateOffsets();//Hack
		}

		internal void Remove (Instruction value)
		{
            List.Remove(value);
            RecalculateOffsets();//Hack
		}

		protected override void OnValidate (object o)
		{
			if (! (o is Instruction))
				throw new ArgumentException ("Must be of type " + typeof (Instruction).FullName);
		}

		public void Accept (ICodeVisitor visitor)
		{
			visitor.VisitInstructionCollection (this);
		}

        internal void RecalculateOffsets()
        {
            int offset = 0;
            for (int i = 0; i < this.Count; i++)
            {
                this[i].Offset = offset;
                this[i].Previous = (i == 0 ? null : this[i - 1]);
                this[i].Next = (i == this.Count - 1 ? null : this[i + 1]);
                offset += this[i].GetSize();
            }
        }
	}
}

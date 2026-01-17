using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EVent.Connections.Models.BaseBinaryConvertables
{
    public class BinaryCovnertableCollection<T> : IBinaryConvertable where T : IBinaryConvertable,new()
    {
        public IEnumerable<T?> binaryConvertables { get; private set; }

        public BinaryCovnertableCollection(IEnumerable<T> binaryCovnertables)
        {
            this.binaryConvertables = binaryCovnertables;
        }

        public BinaryCovnertableCollection()
        {
            this.binaryConvertables = new T[0];
        }

        public bool FromBytes(Span<byte> data)
        {
            int totalLength = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0,4));
            var objects = new T?[totalLength];

            int currentIndex = 4;

            for (int i = 0; i < totalLength; i++)
            {
                int curLen = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(currentIndex, 4));
                currentIndex += 4;

                T item = new T();
                bool sucess = item.FromBytes(data.Slice(currentIndex,curLen));
                currentIndex += curLen;

                objects[i] = sucess ? item : default;
            }
            binaryConvertables = objects;
            return true;
        }

        public byte[] ToBytes()
        {
            var byteArrays = binaryConvertables.Select(x => { 
                if (x != null)
                {
                    return x.ToBytes();
                }
                else
                {
                    return new byte[0];
                }
            }).ToList();
            var ret = new byte[byteArrays.Sum(x=>x.Length) + byteArrays.Count * 4 + 4];
            var byteBuffer = new byte[4];

            BinaryPrimitives.WriteInt32LittleEndian(byteBuffer, byteArrays.Count);
            Buffer.BlockCopy(byteBuffer, 0, ret, 0, 4);

            int currentIndex = 4;

            for (int i = 0; i < byteArrays.Count; i++)
            {
                int len = byteArrays[i].Length;
                BinaryPrimitives.WriteInt32LittleEndian(byteBuffer, len);
                Buffer.BlockCopy(byteBuffer, 0, ret, currentIndex, 4);
                currentIndex += 4;
                Buffer.BlockCopy(byteArrays[i], 0, ret, currentIndex, len);
                currentIndex += len;
            }

            return ret;
        }

        public static implicit operator BinaryCovnertableCollection<T>(List<T> values)
        {
            return new BinaryCovnertableCollection<T>(values);
        }
        public static implicit operator BinaryCovnertableCollection<T>(HashSet<T> values)
        {
            return new BinaryCovnertableCollection<T>(values);
        }
        public static implicit operator BinaryCovnertableCollection<T>(T[] values)
        {
            return new BinaryCovnertableCollection<T>(values);
        }
    }
}

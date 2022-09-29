using System;
using System.Data.HashFunction.xxHash;
using System.Security.Cryptography;

namespace ExtremeFind
{
    public class FileDB
    {
        private const ulong DefaultSeed = 0xa0761d6478bd642f;
        private const ulong ExistFlag = 1;

        private struct KeyValue
        {
            public ulong hash_;
            public string key_;
            public ulong value_;
        }

        public FileDB()
        {
            xxHashConfig hashConfig = new xxHashConfig { HashSizeInBits = 64, Seed = DefaultSeed };
            hash_ = xxHashFactory.Instance.Create(hashConfig);
            capacity_ = 64;
            keyvalues_ = new KeyValue[capacity_];
        }

        public void Add(string key, ulong value)
        {
            byte[] bytes = System.Text.Encoding.Unicode.GetBytes(key);
            ulong hash = BitConverter.ToUInt64(hash_.ComputeHash(bytes).Hash, 0);
            if(!Find(key, hash)) {
                return;
            }
            ulong start = hash & (capacity_ - 1);
            hash |= ExistFlag;
            ulong i = start;
            do {
                if(0 == (keyvalues_[i].hash_ & 0x01)) {
                    keyvalues_[i].hash_ = hash;
                    keyvalues_[i].key_ = key;
                    keyvalues_[i].value_ = value;
                    ++size_;
                    return;
                }
                i = (i + 1) & (capacity_ - 1);
            } while(i != start);
        }

        public void Remove(string key)
        {
            byte[] bytes = System.Text.Encoding.Unicode.GetBytes(key);
            ulong hash = BitConverter.ToUInt64(hash_.ComputeHash(bytes).Hash, 0);
            if(!Find(key, hash)) {
                return;
            }
            ulong start = hash & (capacity_ - 1);
            hash |= ExistFlag;
            ulong i = start;
            do {
                if(keyvalues_[i].hash_ == hash && keyvalues_[i].key_ == key) {
                    keyvalues_[i].hash_ = 0;
                    keyvalues_[i].key_ = string.Empty;
                    keyvalues_[i].value_ = 0;
                    --size_;
                    return;
                }
                i = (i + 1) & (capacity_ - 1);
            } while(i != start);
        }

        public bool TryGet(string key, out ulong value)
        {
            value = 0;
            byte[] bytes = System.Text.Encoding.Unicode.GetBytes(key);
            ulong hash = BitConverter.ToUInt64(hash_.ComputeHash(bytes).Hash, 0);
            ulong start = hash & (capacity_ - 1);
            hash |= ExistFlag;
            ulong i = start;
            do {
                if(keyvalues_[i].hash_ == hash && keyvalues_[i].key_ == key) {
                    value = keyvalues_[i].value_;
                    return true;
                }
                i = (i + 1) & (capacity_ - 1);
            } while(i != start);
            return false;
        }

        private bool Find(string key, ulong hash)
        {
            ulong start = hash & (capacity_ - 1);
            hash |= ExistFlag;
            ulong i = start;
            do {
                if(keyvalues_[i].hash_ == hash && keyvalues_[i].key_ == key) {
                    return true;
                }
                i = (i + 1) & (capacity_ - 1);
            } while(i != start);
            return false;
        }

        public void Serialize(string path)
        {
        }

        public void Deserialize(string path)
        {
        }

        private IxxHash hash_;
        private ulong capacity_;
        private ulong size_;
        private KeyValue[] keyvalues_;
    }
}

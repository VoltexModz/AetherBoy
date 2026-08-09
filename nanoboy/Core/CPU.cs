using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace nanoboy.Core
{

    public sealed class Subscription<T> : IDisposable
    {
        private List<IObserver<T>> observers;
        private IObserver<T> observer;

        public Subscription(List<IObserver<T>> observers, IObserver<T> observer)
        {
            this.observers = observers;
            this.observer = observer;
        }

        public void Dispose()
        {
            if (observers.Contains(observer)) {
                observers.Remove(observer);
            }
        }
    }

    public sealed class CPUStatusUpdate
    {
        public enum UpdateReason
        {
            Exception,
            Execution,
            MemoryRead,
            MemoryWrite,
            Push,
            Pop
        }
        public UpdateReason Reason { get; set; }
        public int Offset { get; set; }
        public int Value { get; set; }
        public CPU CPU { get; set; }
    }

    public sealed class CPU : IObservable<CPUStatusUpdate>
    {

        public bool Running;

        private int a, f, b, c, d, e, h, l, sp, pc;

        public byte A
        {
            get { return (byte)a; }
            set { a = value; }
        }
        public byte F
        {
            get { return (byte)f; }
            set { f = value; }
        }
        public byte B
        {
            get { return (byte)b; }
            set { b = value; }
        }
        public byte C
        {
            get { return (byte)c; }
            set { c = value; }
        }
        public byte D
        {
            get { return (byte)d; }
            set { d = value; }
        }
        public byte E
        {
            get { return (byte)e; }
            set { e = value; }
        }
        public byte H
        {
            get { return (byte)h; }
            set { h = value; }
        }
        public byte L
        {
            get { return (byte)l; }
            set { l = value; }
        }
        public ushort SP
        {
            get { return (ushort)sp; }
            set { sp = value; }
        }
        public ushort PC
        {
            get { return (ushort)pc; }
            set { pc = value; }
        }

        public bool Halt
        {
            get { return WaitForInterrupt; }
            set { WaitForInterrupt = value; }
        }

        public bool FlagC;
        public bool FlagH;
        public bool FlagN;
        public bool FlagZ;
        private bool wroteflagreg;


        public bool IME;
        public bool WaitForInterrupt;
        public bool IsStopped { get; private set; }
        public bool IsLockedUp { get; private set; }
        private int imeEnableDelay;
        private bool haltBug;


        public IMemoryDevice Memory;


        private bool branched = false;
        private int[] cyclesbranched = {4, 12, 8, 8, 4, 4, 8, 4, 20, 8, 8, 8, 4, 4, 8, 4,
                                      4, 12, 8, 8, 4, 4, 8, 4, 12, 8, 8, 8, 4, 4, 8, 4,
                                      12, 12, 8, 8, 4, 4, 8, 4, 12, 8, 8, 8, 4, 4, 8, 4,
                                      12, 12, 8, 8, 12, 12, 12, 4, 12, 8, 8, 8, 4, 4, 8, 4,
                                      4, 4, 4, 4, 4, 4, 8, 4, 4, 4, 4, 4, 4, 4, 8, 4,
                                      4, 4, 4, 4, 4, 4, 8, 4, 4, 4, 4, 4, 4, 4, 8, 4,
                                      4, 4, 4, 4, 4, 4, 8, 4, 4, 4, 4, 4, 4, 4, 8, 4,
                                      4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 8, 4,
                                      4, 4, 4, 4, 4, 4, 8, 4, 4, 4, 4, 4, 4, 4, 8, 4,
                                      4, 4, 4, 4, 4, 4, 8, 4, 4, 4, 4, 4, 4, 4, 8, 4,
                                      4, 4, 4, 4, 4, 4, 8, 4, 4, 4, 4, 4, 4, 4, 8, 4,
                                      4, 4, 4, 4, 4, 4, 8, 4, 4, 4, 4, 4, 4, 4, 8, 4,
                                      20, 12, 16, 16, 24, 16, 8, 16, 20, 16, 16, 4, 24, 24, 8, 16,
                                      20, 12, 16, 16, 24, 16, 8, 16, 20, 16, 16, 4, 24, 24, 8, 16,
                                      12, 12, 8, 0, 0, 16, 8, 16, 16, 4, 16, 0, 0, 0, 8, 16,
                                      12, 12, 8, 4, 0, 16, 8, 16, 12, 8, 16, 4, 0, 0, 8, 16};
        private int[] cycles = {4, 12, 8, 8, 4, 4, 8, 4, 20, 8, 8, 8, 4, 4, 8, 4,
                                    4, 12, 8, 8, 4, 4, 8, 4, 12, 8, 8, 8, 4, 4, 8, 4,
                                    8, 12, 8, 8, 4, 4, 8, 4, 8, 8, 8, 8, 4, 4, 8, 4,
                                    8, 12, 8, 8, 12, 12, 12, 4, 8, 8, 8, 8, 4, 4, 8, 4,
                                    4, 4, 4, 4, 4, 4, 8, 4, 4, 4, 4, 4, 4, 4, 8, 4,
                                    4, 4, 4, 4, 4, 4, 8, 4, 4, 4, 4, 4, 4, 4, 8, 4,
                                    4, 4, 4, 4, 4, 4, 8, 4, 4, 4, 4, 4, 4, 4, 8, 4,
                                    4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 8, 4,
                                    4, 4, 4, 4, 4, 4, 8, 4, 4, 4, 4, 4, 4, 4, 8, 4,
                                    4, 4, 4, 4, 4, 4, 8, 4, 4, 4, 4, 4, 4, 4, 8, 4,
                                    4, 4, 4, 4, 4, 4, 8, 4, 4, 4, 4, 4, 4, 4, 8, 4,
                                    4, 4, 4, 4, 4, 4, 8, 4, 4, 4, 4, 4, 4, 4, 8, 4,
                                    8, 12, 12, 16, 12, 16, 8, 16, 8, 16, 12, 4, 12, 24, 8, 16,
                                    8, 12, 12, 16, 12, 16, 8, 16, 8, 16, 12, 4, 12, 24, 8, 16,
                                    12, 12, 8, 0, 0, 16, 8, 16, 16, 4, 16, 0, 0, 0, 8, 16,
                                    12, 12, 8, 4, 0, 16, 8, 16, 12, 8, 16, 4, 0, 0, 8, 16};
        private int[] cyclesextended = { 8, 8, 8, 8, 8, 8, 16, 8, 8, 8, 8, 8, 8, 8, 16, 8,
                                       8, 8, 8, 8, 8, 8, 16, 8, 8, 8, 8, 8, 8, 8, 16, 8,
                                       8, 8, 8, 8, 8, 8, 16, 8, 8, 8, 8, 8, 8, 8, 16, 8,
                                       8, 8, 8, 8, 8, 8, 16, 8, 8, 8, 8, 8, 8, 8, 16, 8,
                                       8, 8, 8, 8, 8, 8, 16, 8, 8, 8, 8, 8, 8, 8, 16, 8,
                                       8, 8, 8, 8, 8, 8, 16, 8, 8, 8, 8, 8, 8, 8, 16, 8,
                                       8, 8, 8, 8, 8, 8, 16, 8, 8, 8, 8, 8, 8, 8, 16, 8,
                                       8, 8, 8, 8, 8, 8, 16, 8, 8, 8, 8, 8, 8, 8, 16, 8,
                                       8, 8, 8, 8, 8, 8, 16, 8, 8, 8, 8, 8, 8, 8, 16, 8,
                                       8, 8, 8, 8, 8, 8, 16, 8, 8, 8, 8, 8, 8, 8, 16, 8,
                                       8, 8, 8, 8, 8, 8, 16, 8, 8, 8, 8, 8, 8, 8, 16, 8,
                                       8, 8, 8, 8, 8, 8, 16, 8, 8, 8, 8, 8, 8, 8, 16, 8,
                                       8, 8, 8, 8, 8, 8, 16, 8, 8, 8, 8, 8, 8, 8, 16, 8,
                                       8, 8, 8, 8, 8, 8, 16, 8, 8, 8, 8, 8, 8, 8, 16, 8,
                                       8, 8, 8, 8, 8, 8, 16, 8, 8, 8, 8, 8, 8, 8, 16, 8,
                                       8, 8, 8, 8, 8, 8, 16, 8, 8, 8, 8, 8, 8, 8, 16, 8};
        private int cycleamountext;


        private delegate void Instruction();
        private Instruction[] opcode = new Instruction[256];
        private Instruction[] opcode2 = new Instruction[256];


        public bool PrepareSpeedSwitch;
        public bool IsDoubleSpeed;


        private uint[] daatable = new uint[] {
#region DAATABLE
            0x0080,0x0100,0x0200,0x0300,0x0400,0x0500,0x0600,0x0700,
            0x0800,0x0900,0x1020,0x1120,0x1220,0x1320,0x1420,0x1520,
            0x1000,0x1100,0x1200,0x1300,0x1400,0x1500,0x1600,0x1700,
            0x1800,0x1900,0x2020,0x2120,0x2220,0x2320,0x2420,0x2520,
            0x2000,0x2100,0x2200,0x2300,0x2400,0x2500,0x2600,0x2700,
            0x2800,0x2900,0x3020,0x3120,0x3220,0x3320,0x3420,0x3520,
            0x3000,0x3100,0x3200,0x3300,0x3400,0x3500,0x3600,0x3700,
            0x3800,0x3900,0x4020,0x4120,0x4220,0x4320,0x4420,0x4520,
            0x4000,0x4100,0x4200,0x4300,0x4400,0x4500,0x4600,0x4700,
            0x4800,0x4900,0x5020,0x5120,0x5220,0x5320,0x5420,0x5520,
            0x5000,0x5100,0x5200,0x5300,0x5400,0x5500,0x5600,0x5700,
            0x5800,0x5900,0x6020,0x6120,0x6220,0x6320,0x6420,0x6520,
            0x6000,0x6100,0x6200,0x6300,0x6400,0x6500,0x6600,0x6700,
            0x6800,0x6900,0x7020,0x7120,0x7220,0x7320,0x7420,0x7520,
            0x7000,0x7100,0x7200,0x7300,0x7400,0x7500,0x7600,0x7700,
            0x7800,0x7900,0x8020,0x8120,0x8220,0x8320,0x8420,0x8520,
            0x8000,0x8100,0x8200,0x8300,0x8400,0x8500,0x8600,0x8700,
            0x8800,0x8900,0x9020,0x9120,0x9220,0x9320,0x9420,0x9520,
            0x9000,0x9100,0x9200,0x9300,0x9400,0x9500,0x9600,0x9700,
            0x9800,0x9900,0x00B0,0x0130,0x0230,0x0330,0x0430,0x0530,
            0x0090,0x0110,0x0210,0x0310,0x0410,0x0510,0x0610,0x0710,
            0x0810,0x0910,0x1030,0x1130,0x1230,0x1330,0x1430,0x1530,
            0x1010,0x1110,0x1210,0x1310,0x1410,0x1510,0x1610,0x1710,
            0x1810,0x1910,0x2030,0x2130,0x2230,0x2330,0x2430,0x2530,
            0x2010,0x2110,0x2210,0x2310,0x2410,0x2510,0x2610,0x2710,
            0x2810,0x2910,0x3030,0x3130,0x3230,0x3330,0x3430,0x3530,
            0x3010,0x3110,0x3210,0x3310,0x3410,0x3510,0x3610,0x3710,
            0x3810,0x3910,0x4030,0x4130,0x4230,0x4330,0x4430,0x4530,
            0x4010,0x4110,0x4210,0x4310,0x4410,0x4510,0x4610,0x4710,
            0x4810,0x4910,0x5030,0x5130,0x5230,0x5330,0x5430,0x5530,
            0x5010,0x5110,0x5210,0x5310,0x5410,0x5510,0x5610,0x5710,
            0x5810,0x5910,0x6030,0x6130,0x6230,0x6330,0x6430,0x6530,
            0x6010,0x6110,0x6210,0x6310,0x6410,0x6510,0x6610,0x6710,
            0x6810,0x6910,0x7030,0x7130,0x7230,0x7330,0x7430,0x7530,
            0x7010,0x7110,0x7210,0x7310,0x7410,0x7510,0x7610,0x7710,
            0x7810,0x7910,0x8030,0x8130,0x8230,0x8330,0x8430,0x8530,
            0x8010,0x8110,0x8210,0x8310,0x8410,0x8510,0x8610,0x8710,
            0x8810,0x8910,0x9030,0x9130,0x9230,0x9330,0x9430,0x9530,
            0x9010,0x9110,0x9210,0x9310,0x9410,0x9510,0x9610,0x9710,
            0x9810,0x9910,0xA030,0xA130,0xA230,0xA330,0xA430,0xA530,
            0xA010,0xA110,0xA210,0xA310,0xA410,0xA510,0xA610,0xA710,
            0xA810,0xA910,0xB030,0xB130,0xB230,0xB330,0xB430,0xB530,
            0xB010,0xB110,0xB210,0xB310,0xB410,0xB510,0xB610,0xB710,
            0xB810,0xB910,0xC030,0xC130,0xC230,0xC330,0xC430,0xC530,
            0xC010,0xC110,0xC210,0xC310,0xC410,0xC510,0xC610,0xC710,
            0xC810,0xC910,0xD030,0xD130,0xD230,0xD330,0xD430,0xD530,
            0xD010,0xD110,0xD210,0xD310,0xD410,0xD510,0xD610,0xD710,
            0xD810,0xD910,0xE030,0xE130,0xE230,0xE330,0xE430,0xE530,
            0xE010,0xE110,0xE210,0xE310,0xE410,0xE510,0xE610,0xE710,
            0xE810,0xE910,0xF030,0xF130,0xF230,0xF330,0xF430,0xF530,
            0xF010,0xF110,0xF210,0xF310,0xF410,0xF510,0xF610,0xF710,
            0xF810,0xF910,0x00B0,0x0130,0x0230,0x0330,0x0430,0x0530,
            0x0090,0x0110,0x0210,0x0310,0x0410,0x0510,0x0610,0x0710,
            0x0810,0x0910,0x1030,0x1130,0x1230,0x1330,0x1430,0x1530,
            0x1010,0x1110,0x1210,0x1310,0x1410,0x1510,0x1610,0x1710,
            0x1810,0x1910,0x2030,0x2130,0x2230,0x2330,0x2430,0x2530,
            0x2010,0x2110,0x2210,0x2310,0x2410,0x2510,0x2610,0x2710,
            0x2810,0x2910,0x3030,0x3130,0x3230,0x3330,0x3430,0x3530,
            0x3010,0x3110,0x3210,0x3310,0x3410,0x3510,0x3610,0x3710,
            0x3810,0x3910,0x4030,0x4130,0x4230,0x4330,0x4430,0x4530,
            0x4010,0x4110,0x4210,0x4310,0x4410,0x4510,0x4610,0x4710,
            0x4810,0x4910,0x5030,0x5130,0x5230,0x5330,0x5430,0x5530,
            0x5010,0x5110,0x5210,0x5310,0x5410,0x5510,0x5610,0x5710,
            0x5810,0x5910,0x6030,0x6130,0x6230,0x6330,0x6430,0x6530,
            0x0600,0x0700,0x0800,0x0900,0x0A00,0x0B00,0x0C00,0x0D00,
            0x0E00,0x0F00,0x1020,0x1120,0x1220,0x1320,0x1420,0x1520,
            0x1600,0x1700,0x1800,0x1900,0x1A00,0x1B00,0x1C00,0x1D00,
            0x1E00,0x1F00,0x2020,0x2120,0x2220,0x2320,0x2420,0x2520,
            0x2600,0x2700,0x2800,0x2900,0x2A00,0x2B00,0x2C00,0x2D00,
            0x2E00,0x2F00,0x3020,0x3120,0x3220,0x3320,0x3420,0x3520,
            0x3600,0x3700,0x3800,0x3900,0x3A00,0x3B00,0x3C00,0x3D00,
            0x3E00,0x3F00,0x4020,0x4120,0x4220,0x4320,0x4420,0x4520,
            0x4600,0x4700,0x4800,0x4900,0x4A00,0x4B00,0x4C00,0x4D00,
            0x4E00,0x4F00,0x5020,0x5120,0x5220,0x5320,0x5420,0x5520,
            0x5600,0x5700,0x5800,0x5900,0x5A00,0x5B00,0x5C00,0x5D00,
            0x5E00,0x5F00,0x6020,0x6120,0x6220,0x6320,0x6420,0x6520,
            0x6600,0x6700,0x6800,0x6900,0x6A00,0x6B00,0x6C00,0x6D00,
            0x6E00,0x6F00,0x7020,0x7120,0x7220,0x7320,0x7420,0x7520,
            0x7600,0x7700,0x7800,0x7900,0x7A00,0x7B00,0x7C00,0x7D00,
            0x7E00,0x7F00,0x8020,0x8120,0x8220,0x8320,0x8420,0x8520,
            0x8600,0x8700,0x8800,0x8900,0x8A00,0x8B00,0x8C00,0x8D00,
            0x8E00,0x8F00,0x9020,0x9120,0x9220,0x9320,0x9420,0x9520,
            0x9600,0x9700,0x9800,0x9900,0x9A00,0x9B00,0x9C00,0x9D00,
            0x9E00,0x9F00,0x00B0,0x0130,0x0230,0x0330,0x0430,0x0530,
            0x0610,0x0710,0x0810,0x0910,0x0A10,0x0B10,0x0C10,0x0D10,
            0x0E10,0x0F10,0x1030,0x1130,0x1230,0x1330,0x1430,0x1530,
            0x1610,0x1710,0x1810,0x1910,0x1A10,0x1B10,0x1C10,0x1D10,
            0x1E10,0x1F10,0x2030,0x2130,0x2230,0x2330,0x2430,0x2530,
            0x2610,0x2710,0x2810,0x2910,0x2A10,0x2B10,0x2C10,0x2D10,
            0x2E10,0x2F10,0x3030,0x3130,0x3230,0x3330,0x3430,0x3530,
            0x3610,0x3710,0x3810,0x3910,0x3A10,0x3B10,0x3C10,0x3D10,
            0x3E10,0x3F10,0x4030,0x4130,0x4230,0x4330,0x4430,0x4530,
            0x4610,0x4710,0x4810,0x4910,0x4A10,0x4B10,0x4C10,0x4D10,
            0x4E10,0x4F10,0x5030,0x5130,0x5230,0x5330,0x5430,0x5530,
            0x5610,0x5710,0x5810,0x5910,0x5A10,0x5B10,0x5C10,0x5D10,
            0x5E10,0x5F10,0x6030,0x6130,0x6230,0x6330,0x6430,0x6530,
            0x6610,0x6710,0x6810,0x6910,0x6A10,0x6B10,0x6C10,0x6D10,
            0x6E10,0x6F10,0x7030,0x7130,0x7230,0x7330,0x7430,0x7530,
            0x7610,0x7710,0x7810,0x7910,0x7A10,0x7B10,0x7C10,0x7D10,
            0x7E10,0x7F10,0x8030,0x8130,0x8230,0x8330,0x8430,0x8530,
            0x8610,0x8710,0x8810,0x8910,0x8A10,0x8B10,0x8C10,0x8D10,
            0x8E10,0x8F10,0x9030,0x9130,0x9230,0x9330,0x9430,0x9530,
            0x9610,0x9710,0x9810,0x9910,0x9A10,0x9B10,0x9C10,0x9D10,
            0x9E10,0x9F10,0xA030,0xA130,0xA230,0xA330,0xA430,0xA530,
            0xA610,0xA710,0xA810,0xA910,0xAA10,0xAB10,0xAC10,0xAD10,
            0xAE10,0xAF10,0xB030,0xB130,0xB230,0xB330,0xB430,0xB530,
            0xB610,0xB710,0xB810,0xB910,0xBA10,0xBB10,0xBC10,0xBD10,
            0xBE10,0xBF10,0xC030,0xC130,0xC230,0xC330,0xC430,0xC530,
            0xC610,0xC710,0xC810,0xC910,0xCA10,0xCB10,0xCC10,0xCD10,
            0xCE10,0xCF10,0xD030,0xD130,0xD230,0xD330,0xD430,0xD530,
            0xD610,0xD710,0xD810,0xD910,0xDA10,0xDB10,0xDC10,0xDD10,
            0xDE10,0xDF10,0xE030,0xE130,0xE230,0xE330,0xE430,0xE530,
            0xE610,0xE710,0xE810,0xE910,0xEA10,0xEB10,0xEC10,0xED10,
            0xEE10,0xEF10,0xF030,0xF130,0xF230,0xF330,0xF430,0xF530,
            0xF610,0xF710,0xF810,0xF910,0xFA10,0xFB10,0xFC10,0xFD10,
            0xFE10,0xFF10,0x00B0,0x0130,0x0230,0x0330,0x0430,0x0530,
            0x0610,0x0710,0x0810,0x0910,0x0A10,0x0B10,0x0C10,0x0D10,
            0x0E10,0x0F10,0x1030,0x1130,0x1230,0x1330,0x1430,0x1530,
            0x1610,0x1710,0x1810,0x1910,0x1A10,0x1B10,0x1C10,0x1D10,
            0x1E10,0x1F10,0x2030,0x2130,0x2230,0x2330,0x2430,0x2530,
            0x2610,0x2710,0x2810,0x2910,0x2A10,0x2B10,0x2C10,0x2D10,
            0x2E10,0x2F10,0x3030,0x3130,0x3230,0x3330,0x3430,0x3530,
            0x3610,0x3710,0x3810,0x3910,0x3A10,0x3B10,0x3C10,0x3D10,
            0x3E10,0x3F10,0x4030,0x4130,0x4230,0x4330,0x4430,0x4530,
            0x4610,0x4710,0x4810,0x4910,0x4A10,0x4B10,0x4C10,0x4D10,
            0x4E10,0x4F10,0x5030,0x5130,0x5230,0x5330,0x5430,0x5530,
            0x5610,0x5710,0x5810,0x5910,0x5A10,0x5B10,0x5C10,0x5D10,
            0x5E10,0x5F10,0x6030,0x6130,0x6230,0x6330,0x6430,0x6530,
            0x00C0,0x0140,0x0240,0x0340,0x0440,0x0540,0x0640,0x0740,
            0x0840,0x0940,0x0440,0x0540,0x0640,0x0740,0x0840,0x0940,
            0x1040,0x1140,0x1240,0x1340,0x1440,0x1540,0x1640,0x1740,
            0x1840,0x1940,0x1440,0x1540,0x1640,0x1740,0x1840,0x1940,
            0x2040,0x2140,0x2240,0x2340,0x2440,0x2540,0x2640,0x2740,
            0x2840,0x2940,0x2440,0x2540,0x2640,0x2740,0x2840,0x2940,
            0x3040,0x3140,0x3240,0x3340,0x3440,0x3540,0x3640,0x3740,
            0x3840,0x3940,0x3440,0x3540,0x3640,0x3740,0x3840,0x3940,
            0x4040,0x4140,0x4240,0x4340,0x4440,0x4540,0x4640,0x4740,
            0x4840,0x4940,0x4440,0x4540,0x4640,0x4740,0x4840,0x4940,
            0x5040,0x5140,0x5240,0x5340,0x5440,0x5540,0x5640,0x5740,
            0x5840,0x5940,0x5440,0x5540,0x5640,0x5740,0x5840,0x5940,
            0x6040,0x6140,0x6240,0x6340,0x6440,0x6540,0x6640,0x6740,
            0x6840,0x6940,0x6440,0x6540,0x6640,0x6740,0x6840,0x6940,
            0x7040,0x7140,0x7240,0x7340,0x7440,0x7540,0x7640,0x7740,
            0x7840,0x7940,0x7440,0x7540,0x7640,0x7740,0x7840,0x7940,
            0x8040,0x8140,0x8240,0x8340,0x8440,0x8540,0x8640,0x8740,
            0x8840,0x8940,0x8440,0x8540,0x8640,0x8740,0x8840,0x8940,
            0x9040,0x9140,0x9240,0x9340,0x9440,0x9540,0x9640,0x9740,
            0x9840,0x9940,0x3450,0x3550,0x3650,0x3750,0x3850,0x3950,
            0x4050,0x4150,0x4250,0x4350,0x4450,0x4550,0x4650,0x4750,
            0x4850,0x4950,0x4450,0x4550,0x4650,0x4750,0x4850,0x4950,
            0x5050,0x5150,0x5250,0x5350,0x5450,0x5550,0x5650,0x5750,
            0x5850,0x5950,0x5450,0x5550,0x5650,0x5750,0x5850,0x5950,
            0x6050,0x6150,0x6250,0x6350,0x6450,0x6550,0x6650,0x6750,
            0x6850,0x6950,0x6450,0x6550,0x6650,0x6750,0x6850,0x6950,
            0x7050,0x7150,0x7250,0x7350,0x7450,0x7550,0x7650,0x7750,
            0x7850,0x7950,0x7450,0x7550,0x7650,0x7750,0x7850,0x7950,
            0x8050,0x8150,0x8250,0x8350,0x8450,0x8550,0x8650,0x8750,
            0x8850,0x8950,0x8450,0x8550,0x8650,0x8750,0x8850,0x8950,
            0x9050,0x9150,0x9250,0x9350,0x9450,0x9550,0x9650,0x9750,
            0x9850,0x9950,0x9450,0x9550,0x9650,0x9750,0x9850,0x9950,
            0xA050,0xA150,0xA250,0xA350,0xA450,0xA550,0xA650,0xA750,
            0xA850,0xA950,0xA450,0xA550,0xA650,0xA750,0xA850,0xA950,
            0xB050,0xB150,0xB250,0xB350,0xB450,0xB550,0xB650,0xB750,
            0xB850,0xB950,0xB450,0xB550,0xB650,0xB750,0xB850,0xB950,
            0xC050,0xC150,0xC250,0xC350,0xC450,0xC550,0xC650,0xC750,
            0xC850,0xC950,0xC450,0xC550,0xC650,0xC750,0xC850,0xC950,
            0xD050,0xD150,0xD250,0xD350,0xD450,0xD550,0xD650,0xD750,
            0xD850,0xD950,0xD450,0xD550,0xD650,0xD750,0xD850,0xD950,
            0xE050,0xE150,0xE250,0xE350,0xE450,0xE550,0xE650,0xE750,
            0xE850,0xE950,0xE450,0xE550,0xE650,0xE750,0xE850,0xE950,
            0xF050,0xF150,0xF250,0xF350,0xF450,0xF550,0xF650,0xF750,
            0xF850,0xF950,0xF450,0xF550,0xF650,0xF750,0xF850,0xF950,
            0x00D0,0x0150,0x0250,0x0350,0x0450,0x0550,0x0650,0x0750,
            0x0850,0x0950,0x0450,0x0550,0x0650,0x0750,0x0850,0x0950,
            0x1050,0x1150,0x1250,0x1350,0x1450,0x1550,0x1650,0x1750,
            0x1850,0x1950,0x1450,0x1550,0x1650,0x1750,0x1850,0x1950,
            0x2050,0x2150,0x2250,0x2350,0x2450,0x2550,0x2650,0x2750,
            0x2850,0x2950,0x2450,0x2550,0x2650,0x2750,0x2850,0x2950,
            0x3050,0x3150,0x3250,0x3350,0x3450,0x3550,0x3650,0x3750,
            0x3850,0x3950,0x3450,0x3550,0x3650,0x3750,0x3850,0x3950,
            0x4050,0x4150,0x4250,0x4350,0x4450,0x4550,0x4650,0x4750,
            0x4850,0x4950,0x4450,0x4550,0x4650,0x4750,0x4850,0x4950,
            0x5050,0x5150,0x5250,0x5350,0x5450,0x5550,0x5650,0x5750,
            0x5850,0x5950,0x5450,0x5550,0x5650,0x5750,0x5850,0x5950,
            0x6050,0x6150,0x6250,0x6350,0x6450,0x6550,0x6650,0x6750,
            0x6850,0x6950,0x6450,0x6550,0x6650,0x6750,0x6850,0x6950,
            0x7050,0x7150,0x7250,0x7350,0x7450,0x7550,0x7650,0x7750,
            0x7850,0x7950,0x7450,0x7550,0x7650,0x7750,0x7850,0x7950,
            0x8050,0x8150,0x8250,0x8350,0x8450,0x8550,0x8650,0x8750,
            0x8850,0x8950,0x8450,0x8550,0x8650,0x8750,0x8850,0x8950,
            0x9050,0x9150,0x9250,0x9350,0x9450,0x9550,0x9650,0x9750,
            0x9850,0x9950,0x9450,0x9550,0x9650,0x9750,0x9850,0x9950,
            0xFA60,0xFB60,0xFC60,0xFD60,0xFE60,0xFF60,0x00C0,0x0140,
            0x0240,0x0340,0x0440,0x0540,0x0640,0x0740,0x0840,0x0940,
            0x0A60,0x0B60,0x0C60,0x0D60,0x0E60,0x0F60,0x1040,0x1140,
            0x1240,0x1340,0x1440,0x1540,0x1640,0x1740,0x1840,0x1940,
            0x1A60,0x1B60,0x1C60,0x1D60,0x1E60,0x1F60,0x2040,0x2140,
            0x2240,0x2340,0x2440,0x2540,0x2640,0x2740,0x2840,0x2940,
            0x2A60,0x2B60,0x2C60,0x2D60,0x2E60,0x2F60,0x3040,0x3140,
            0x3240,0x3340,0x3440,0x3540,0x3640,0x3740,0x3840,0x3940,
            0x3A60,0x3B60,0x3C60,0x3D60,0x3E60,0x3F60,0x4040,0x4140,
            0x4240,0x4340,0x4440,0x4540,0x4640,0x4740,0x4840,0x4940,
            0x4A60,0x4B60,0x4C60,0x4D60,0x4E60,0x4F60,0x5040,0x5140,
            0x5240,0x5340,0x5440,0x5540,0x5640,0x5740,0x5840,0x5940,
            0x5A60,0x5B60,0x5C60,0x5D60,0x5E60,0x5F60,0x6040,0x6140,
            0x6240,0x6340,0x6440,0x6540,0x6640,0x6740,0x6840,0x6940,
            0x6A60,0x6B60,0x6C60,0x6D60,0x6E60,0x6F60,0x7040,0x7140,
            0x7240,0x7340,0x7440,0x7540,0x7640,0x7740,0x7840,0x7940,
            0x7A60,0x7B60,0x7C60,0x7D60,0x7E60,0x7F60,0x8040,0x8140,
            0x8240,0x8340,0x8440,0x8540,0x8640,0x8740,0x8840,0x8940,
            0x8A60,0x8B60,0x8C60,0x8D60,0x8E60,0x8F60,0x9040,0x9140,
            0x9240,0x9340,0x3450,0x3550,0x3650,0x3750,0x3850,0x3950,
            0x3A70,0x3B70,0x3C70,0x3D70,0x3E70,0x3F70,0x4050,0x4150,
            0x4250,0x4350,0x4450,0x4550,0x4650,0x4750,0x4850,0x4950,
            0x4A70,0x4B70,0x4C70,0x4D70,0x4E70,0x4F70,0x5050,0x5150,
            0x5250,0x5350,0x5450,0x5550,0x5650,0x5750,0x5850,0x5950,
            0x5A70,0x5B70,0x5C70,0x5D70,0x5E70,0x5F70,0x6050,0x6150,
            0x6250,0x6350,0x6450,0x6550,0x6650,0x6750,0x6850,0x6950,
            0x6A70,0x6B70,0x6C70,0x6D70,0x6E70,0x6F70,0x7050,0x7150,
            0x7250,0x7350,0x7450,0x7550,0x7650,0x7750,0x7850,0x7950,
            0x7A70,0x7B70,0x7C70,0x7D70,0x7E70,0x7F70,0x8050,0x8150,
            0x8250,0x8350,0x8450,0x8550,0x8650,0x8750,0x8850,0x8950,
            0x8A70,0x8B70,0x8C70,0x8D70,0x8E70,0x8F70,0x9050,0x9150,
            0x9250,0x9350,0x9450,0x9550,0x9650,0x9750,0x9850,0x9950,
            0x9A70,0x9B70,0x9C70,0x9D70,0x9E70,0x9F70,0xA050,0xA150,
            0xA250,0xA350,0xA450,0xA550,0xA650,0xA750,0xA850,0xA950,
            0xAA70,0xAB70,0xAC70,0xAD70,0xAE70,0xAF70,0xB050,0xB150,
            0xB250,0xB350,0xB450,0xB550,0xB650,0xB750,0xB850,0xB950,
            0xBA70,0xBB70,0xBC70,0xBD70,0xBE70,0xBF70,0xC050,0xC150,
            0xC250,0xC350,0xC450,0xC550,0xC650,0xC750,0xC850,0xC950,
            0xCA70,0xCB70,0xCC70,0xCD70,0xCE70,0xCF70,0xD050,0xD150,
            0xD250,0xD350,0xD450,0xD550,0xD650,0xD750,0xD850,0xD950,
            0xDA70,0xDB70,0xDC70,0xDD70,0xDE70,0xDF70,0xE050,0xE150,
            0xE250,0xE350,0xE450,0xE550,0xE650,0xE750,0xE850,0xE950,
            0xEA70,0xEB70,0xEC70,0xED70,0xEE70,0xEF70,0xF050,0xF150,
            0xF250,0xF350,0xF450,0xF550,0xF650,0xF750,0xF850,0xF950,
            0xFA70,0xFB70,0xFC70,0xFD70,0xFE70,0xFF70,0x00D0,0x0150,
            0x0250,0x0350,0x0450,0x0550,0x0650,0x0750,0x0850,0x0950,
            0x0A70,0x0B70,0x0C70,0x0D70,0x0E70,0x0F70,0x1050,0x1150,
            0x1250,0x1350,0x1450,0x1550,0x1650,0x1750,0x1850,0x1950,
            0x1A70,0x1B70,0x1C70,0x1D70,0x1E70,0x1F70,0x2050,0x2150,
            0x2250,0x2350,0x2450,0x2550,0x2650,0x2750,0x2850,0x2950,
            0x2A70,0x2B70,0x2C70,0x2D70,0x2E70,0x2F70,0x3050,0x3150,
            0x3250,0x3350,0x3450,0x3550,0x3650,0x3750,0x3850,0x3950,
            0x3A70,0x3B70,0x3C70,0x3D70,0x3E70,0x3F70,0x4050,0x4150,
            0x4250,0x4350,0x4450,0x4550,0x4650,0x4750,0x4850,0x4950,
            0x4A70,0x4B70,0x4C70,0x4D70,0x4E70,0x4F70,0x5050,0x5150,
            0x5250,0x5350,0x5450,0x5550,0x5650,0x5750,0x5850,0x5950,
            0x5A70,0x5B70,0x5C70,0x5D70,0x5E70,0x5F70,0x6050,0x6150,
            0x6250,0x6350,0x6450,0x6550,0x6650,0x6750,0x6850,0x6950,
            0x6A70,0x6B70,0x6C70,0x6D70,0x6E70,0x6F70,0x7050,0x7150,
            0x7250,0x7350,0x7450,0x7550,0x7650,0x7750,0x7850,0x7950,
            0x7A70,0x7B70,0x7C70,0x7D70,0x7E70,0x7F70,0x8050,0x8150,
            0x8250,0x8350,0x8450,0x8550,0x8650,0x8750,0x8850,0x8950,
            0x8A70,0x8B70,0x8C70,0x8D70,0x8E70,0x8F70,0x9050,0x9150,
            0x9250,0x9350,0x9450,0x9550,0x9650,0x9750,0x9850,0x9950
#endregion
        };


        private List<IObserver<CPUStatusUpdate>> observers;

        public CPU()
        {
            Running = true;
            IME = false;
            #region Opcode table filling

            //Folgende Argumente müssen nicht verständlich sein! Das sind nur Hilfen für mich!

                opcode[0x00] = delegate() { // NOP
                    pc++;
                    };
                opcode[0x01] = delegate() { // LD BC, d16
                    OP_LD_R16_D16(ref b, ref c);
                    };
                opcode[0x02] = delegate() { // LD (BC), A
                    OP_LD_PR16_R8(b, c, a);
                    };
                opcode[0x03] = delegate() { // INC BC
                    OP_INC_R16(ref b, ref c);
                    };
                opcode[0x04] = delegate() { // INC B
                    OP_INC_R8(ref b);
                    };
                opcode[0x05] = delegate() { // DEC B
                    OP_DEC_R8(ref b);
                    };
                opcode[0x06] = delegate() { // LD B, D8
                    OP_LD_R8_D8(ref b);
                    };
                opcode[0x07] = delegate() { // RLCA
                    OP_RLCA();
                    };
                opcode[0x08] = delegate() { // LD (A16), SP
                    OP_LD_A16_R16((sp >> 8) & 0xFF, sp & 0xFF);
                    };
                opcode[0x09] = delegate() { // ADD HL, BC
                    OP_ADD_R16_R16(ref h, ref l, b, c);
                    };
                opcode[0x0A] = delegate() { // LD A, (BC)
                    OP_LD_R8_PR16(ref a, b, c);
                    };
                opcode[0x0B] = delegate() { // DEC BC
                    OP_DEC_R16(ref b, ref c);
                    };
                opcode[0x0C] = delegate() { // INC C
                    OP_INC_R8(ref c);
                    };
                opcode[0x0D] = delegate() { // DEC C
                    OP_DEC_R8(ref c);
                    };
                opcode[0x0E] = delegate() { // LD C, D8
                    OP_LD_R8_D8(ref c);
                    };
                opcode[0x0F] = delegate() { // RRCA
                    OP_RRCA();
                    };
                opcode[0x10] = delegate() { // STOP 0
                    if (PrepareSpeedSwitch) {
                        IsDoubleSpeed = !IsDoubleSpeed;
                        PrepareSpeedSwitch = false;
                    } else {
                        IsStopped = true;
                    }
                    pc += 2;
                    };
                opcode[0x11] = delegate() { // LD DE, D16
                    OP_LD_R16_D16(ref d, ref e);
                    };
                opcode[0x12] = delegate() { // LD (DE), A
                    OP_LD_PR16_R8(d, e, a);
                    };
                opcode[0x13] = delegate() { // INC DE
                    OP_INC_R16(ref d, ref e);
                    };
                opcode[0x14] = delegate() { // INC D
                    OP_INC_R8(ref d);
                    };
                opcode[0x15] = delegate() { // DEC D
                    OP_DEC_R8(ref d);
                    };
                opcode[0x16] = delegate() { // LD D, D8
                    OP_LD_R8_D8(ref d);
                    };
                opcode[0x17] = delegate() { // RLA
                    OP_RLA();
                    };
                opcode[0x18] = delegate() { // JR S8
                    OP_JR_S8();
                    };
                opcode[0x19] = delegate() { // ADD HL, DE
                    OP_ADD_R16_R16(ref h, ref l, d, e);
                    };
                opcode[0x1A] = delegate() { // LD A, (DE)
                    OP_LD_R8_PR16(ref a, d, e);
                    };
                opcode[0x1B] = delegate() { // DEC DE
                    OP_DEC_R16(ref d, ref e);
                    };
                opcode[0x1C] = delegate() { // INC E
                    OP_INC_R8(ref e);
                    };
                opcode[0x1D] = delegate() { // DEC E
                    OP_DEC_R8(ref e);
                    };
                opcode[0x1E] = delegate() { // LD E, D8
                    OP_LD_R8_D8(ref e);
                    };
                opcode[0x1F] = delegate() { // RRA
                    OP_RRA();
                    };
                opcode[0x20] = delegate() { // JR NZ, S8
                    OP_JRNZ_S8();
                    };
                opcode[0x21] = delegate() { // LD HL, D16
                    OP_LD_R16_D16(ref h, ref l);
                    };
                opcode[0x22] = delegate() { // LD (HL+), A
                    OP_LDI_PR16_R8(ref h, ref l, a);
                    };
                opcode[0x23] = delegate() { // INC HL
                    OP_INC_R16(ref h, ref l);
                    };
                opcode[0x24] = delegate() { // INC H
                    OP_INC_R8(ref h);
                    };
                opcode[0x25] = delegate() { // DEC H
                    OP_DEC_R8(ref h);
                    };
                opcode[0x26] = delegate() { // LD H, D8
                    OP_LD_R8_D8(ref h);
                    };
                opcode[0x27] = delegate() { // DAA
                    OP_DAA();
                    };
                opcode[0x28] = delegate() { // JR Z, S8
                    OP_JRZ_S8();
                    };
                opcode[0x29] = delegate() { // ADD HL, HL
                    OP_ADD_R16_R16(ref h, ref l, h, l);
                    };
                opcode[0x2A] = delegate() { // LD A, (HL+)
                    OP_LDI_R8_PR16(ref a, ref h, ref l);
                    };
                opcode[0x2B] = delegate() { // DEC HL
                    OP_DEC_R16(ref h, ref l);
                    };
                opcode[0x2C] = delegate() { // INC L
                    OP_INC_R8(ref l);
                    };
                opcode[0x2D] = delegate() { // DEC L
                    OP_DEC_R8(ref l);
                    };
                opcode[0x2E] = delegate() { // LD L, D8
                    OP_LD_R8_D8(ref l);
                    };
                opcode[0x2F] = delegate() { // CPL
                    OP_CPL();
                    };
                opcode[0x30] = delegate() { // JR NC, S8
                    OP_JRNC_S8();
                    };
                opcode[0x31] = delegate() { // LD SP, D16
                    OP_LD_SP_D16();
                    };
                opcode[0x32] = delegate() { // LD (HL-), A
                    OP_LDD_PR16_R8(ref h, ref l, a);
                    };
                opcode[0x33] = delegate() { // INC SP
                    OP_INC_SP();
                    };
                opcode[0x34] = delegate() { // INC (HL)
                    OP_INC_PR16(h, l);
                    };
                opcode[0x35] = delegate() { // DEC (HL)
                    OP_DEC_PR16(h, l);
                    };
                opcode[0x36] = delegate() { // LD (HL), D8
                    OP_LD_PR16_D8(h, l);
                    };
                opcode[0x37] = delegate() { // SCF
                    OP_SCF();
                    };
                opcode[0x38] = delegate() { // JR C, S8
                    OP_JRC_S8();
                    };
                opcode[0x39] = delegate() { // ADD HL, SP
                    OP_ADD_R16_R16(ref h, ref l, sp >> 8, sp & 0xFF);
                    };
                opcode[0x3A] = delegate() { // LD A, (HL-)
                    OP_LDD_R8_PR16(ref a, ref h, ref l);
                    };
                opcode[0x3B] = delegate() { // DEC SP
                    sp--;
                    sp &= 0xFFFF;
                    pc++;
                    };
                opcode[0x3C] = delegate() { // INC A
                    OP_INC_R8(ref a);
                    };
                opcode[0x3D] = delegate() { // DEC A
                    OP_DEC_R8(ref a);
                    };
                opcode[0x3E] = delegate() { // LD A, D8
                    OP_LD_R8_D8(ref a);
                    };
                opcode[0x3F] = delegate() { // CCF
                    OP_CCF();
                    };
                opcode[0x40] = delegate() { // LD B, B
                    OP_LD_R8_R8(ref b, b);
                    };
                opcode[0x41] = delegate() { // LD B, C
                    OP_LD_R8_R8(ref b, c);
                    };
                opcode[0x42] = delegate() { // LD B, D
                    OP_LD_R8_R8(ref b, d);
                    };
                opcode[0x43] = delegate() { // LD B, E
                    OP_LD_R8_R8(ref b, e);
                    };
                opcode[0x44] = delegate() { // LD B, H
                    OP_LD_R8_R8(ref b, h);
                    };
                opcode[0x45] = delegate() { // LD B, L
                    OP_LD_R8_R8(ref b, l);
                    };
                opcode[0x46] = delegate() { // LD B, (HL)
                    OP_LD_R8_PR16(ref b, h, l);
                    };
                opcode[0x47] = delegate() { // LD B, A
                    OP_LD_R8_R8(ref b, a);
                    };
                opcode[0x48] = delegate() { // LD C, B
                    OP_LD_R8_R8(ref c, b);
                    };
                opcode[0x49] = delegate() { // LD C, C
                    OP_LD_R8_R8(ref c, c);
                    };
                opcode[0x4A] = delegate() { // LD C, D
                    OP_LD_R8_R8(ref c, d);
                    };
                opcode[0x4B] = delegate() { // LD C, E
                    OP_LD_R8_R8(ref c, e);
                    };
                opcode[0x4C] = delegate() { // LD C, H
                    OP_LD_R8_R8(ref c, h);
                    };
                opcode[0x4D] = delegate() { // LD C, L
                    OP_LD_R8_R8(ref c, l);
                    };
                opcode[0x4E] = delegate() { // LD C, (HL)
                    OP_LD_R8_PR16(ref c, h, l);
                    };
                opcode[0x4F] = delegate() { // LD C, A
                    OP_LD_R8_R8(ref c, a);
                    };
                opcode[0x50] = delegate() { // LD D, B
                    OP_LD_R8_R8(ref d, b);
                    };
                opcode[0x51] = delegate() { // LD D, C
                    OP_LD_R8_R8(ref d, c);
                    };
                opcode[0x52] = delegate() { // LD D, D
                    OP_LD_R8_R8(ref d, d);
                    };
                opcode[0x53] = delegate() { // LD D, E
                    OP_LD_R8_R8(ref d, e);
                    };
                opcode[0x54] = delegate() { // LD D, H
                    OP_LD_R8_R8(ref d, h);
                    };
                opcode[0x55] = delegate() { // LD D, L
                    OP_LD_R8_R8(ref d, l);
                    };
                opcode[0x56] = delegate() { // LD D, (HL)
                    OP_LD_R8_PR16(ref d, h, l);
                    };
                opcode[0x57] = delegate() { // LD D, A
                    OP_LD_R8_R8(ref d, a);
                    };
                opcode[0x58] = delegate() { // LD E, B
                    OP_LD_R8_R8(ref e, b);
                    };
                opcode[0x59] = delegate() { // LD E, C
                    OP_LD_R8_R8(ref e, c);
                    };
                opcode[0x5A] = delegate() { // LD E, D
                    OP_LD_R8_R8(ref e, d);
                    };
                opcode[0x5B] = delegate() { // LD E, E
                    OP_LD_R8_R8(ref e, e);
                    };
                opcode[0x5C] = delegate() { // LD E, H
                    OP_LD_R8_R8(ref e, h);
                    };
                opcode[0x5D] = delegate() { // LD E, L
                    OP_LD_R8_R8(ref e, l);
                    };
                opcode[0x5E] = delegate() { // LD E, (HL)
                    OP_LD_R8_PR16(ref e, h, l);
                    };
                opcode[0x5F] = delegate() { // LD E, A
                    OP_LD_R8_R8(ref e, a);
                    };
                opcode[0x60] = delegate() { // LD H, B
                    OP_LD_R8_R8(ref h, b);
                    };
                opcode[0x61] = delegate() { // LD H, C
                    OP_LD_R8_R8(ref h, c);
                    };
                opcode[0x62] = delegate() { // LD H, D
                    OP_LD_R8_R8(ref h, d);
                    };
                opcode[0x63] = delegate() { // LD H, E
                    OP_LD_R8_R8(ref h, e);
                    };
                opcode[0x64] = delegate() { // LD H, H
                    OP_LD_R8_R8(ref h, h);
                    };
                opcode[0x65] = delegate() { // LD H, L
                    OP_LD_R8_R8(ref h, l);
                    };
                opcode[0x66] = delegate() { // LD H, (HL)
                    OP_LD_R8_PR16(ref h, h, l);
                    };
                opcode[0x67] = delegate() { // LD H, A
                    OP_LD_R8_R8(ref h, a);
                    };
                opcode[0x68] = delegate() { // LD L, B
                    OP_LD_R8_R8(ref l, b);
                    };
                opcode[0x69] = delegate() { // LD L, C
                    OP_LD_R8_R8(ref l, c);
                    };
                opcode[0x6A] = delegate() { // LD L, D
                    OP_LD_R8_R8(ref l, d);
                    };
                opcode[0x6B] = delegate() { // LD L, E
                    OP_LD_R8_R8(ref l, e);
                    };
                opcode[0x6C] = delegate() { // LD L, H
                    OP_LD_R8_R8(ref l, h);
                    };
                opcode[0x6D] = delegate() { // LD L, L
                    OP_LD_R8_R8(ref l, l);
                    };
                opcode[0x6E] = delegate() { // LD L, (HL)
                    OP_LD_R8_PR16(ref l, h, l);
                    };
                opcode[0x6F] = delegate() { // LD L, A
                    OP_LD_R8_R8(ref l, a);
                    };
                opcode[0x70] = delegate() { // LD (HL), B
                    OP_LD_PR16_R8(h, l, b);
                    };
                opcode[0x71] = delegate() { // LD (HL), C
                    OP_LD_PR16_R8(h, l, c);
                    };
                opcode[0x72] = delegate() { // LD (HL), D
                    OP_LD_PR16_R8(h, l, d);
                    };
                opcode[0x73] = delegate() { // LD (HL), E
                    OP_LD_PR16_R8(h, l, e);
                    };
                opcode[0x74] = delegate() { // LD (HL), H
                    OP_LD_PR16_R8(h, l, h);
                    };
                opcode[0x75] = delegate() { // LD (HL), L
                    OP_LD_PR16_R8(h, l, l);
                    };
                opcode[0x76] = delegate() { // HALT
                    OP_HALT();
                    };
                opcode[0x77] = delegate() { // LD (HL), A
                    OP_LD_PR16_R8(h, l, a);
                    };
                opcode[0x78] = delegate() { // LD A, B
                    OP_LD_R8_R8(ref a, b);
                    };
                opcode[0x79] = delegate() { // LD A, C
                    OP_LD_R8_R8(ref a, c);
                    };
                opcode[0x7A] = delegate() { // LD A, D
                    OP_LD_R8_R8(ref a, d);
                    };
                opcode[0x7B] = delegate() { // LD A, E
                    OP_LD_R8_R8(ref a, e);
                    };
                opcode[0x7C] = delegate() { // LD A, H
                    OP_LD_R8_R8(ref a, h);
                    };
                opcode[0x7D] = delegate() { // LD A, L
                    OP_LD_R8_R8(ref a, l);
                    };
                opcode[0x7E] = delegate() { // LD A, (HL)
                    OP_LD_R8_PR16(ref a, h, l);
                    };
                opcode[0x7F] = delegate() { // LD A, A
                    OP_LD_R8_R8(ref a, a);
                    };
                opcode[0x80] = delegate() { // ADD A, B
                    OP_ADD_R8_R8(ref a, b);
                    };
                opcode[0x81] = delegate() { // ADD A, C
                    OP_ADD_R8_R8(ref a, c);
                    };
                opcode[0x82] = delegate() { // ADD A, D
                    OP_ADD_R8_R8(ref a, d);
                    };
                opcode[0x83] = delegate() { // ADD A, E
                    OP_ADD_R8_R8(ref a, e);
                    };
                opcode[0x84] = delegate() { // ADD A, H
                    OP_ADD_R8_R8(ref a, h);
                    };
                opcode[0x85] = delegate() { // ADD A, L
                    OP_ADD_R8_R8(ref a, l);
                    };
                opcode[0x86] = delegate() { // ADD A, (HL)
                    OP_ADD_R8_PR16(ref a, h, l);
                    };
                opcode[0x87] = delegate() { // ADD A, A
                    OP_ADD_R8_R8(ref a, a);
                    };
                opcode[0x88] = delegate() { // ADC A, B
                    OP_ADC_R8_R8(ref a, b);
                    };
                opcode[0x89] = delegate() { // ADC A, C
                    OP_ADC_R8_R8(ref a, c);
                    };
                opcode[0x8A] = delegate() { // ADC A, D
                    OP_ADC_R8_R8(ref a, d);
                    };
                opcode[0x8B] = delegate() { // ADC A, E
                    OP_ADC_R8_R8(ref a, e);
                    };
                opcode[0x8C] = delegate() { // ADC A, H
                    OP_ADC_R8_R8(ref a, h);
                    };
                opcode[0x8D] = delegate() { // ADC A, L
                    OP_ADC_R8_R8(ref a, l);
                    };
                opcode[0x8E] = delegate() { // ADC A, (HL)
                    OP_ADC_R8_PR16(ref a, h, l);
                    };
                opcode[0x8F] = delegate() { // ADC A, A
                    OP_ADC_R8_R8(ref a, a);
                    };
                opcode[0x90] = delegate() { // SUB B
                    OP_SUB_R8(b);
                    };
                opcode[0x91] = delegate() { // SUB C
                    OP_SUB_R8(c);
                    };
                opcode[0x92] = delegate() { // SUB D
                    OP_SUB_R8(d);
                    };
                opcode[0x93] = delegate() { // SUB E
                    OP_SUB_R8(e);
                    };
                opcode[0x94] = delegate() { // SUB H
                    OP_SUB_R8(h);
                    };
                opcode[0x95] = delegate() { // SUB L
                    OP_SUB_R8(l);
                    };
                opcode[0x96] = delegate() { // SUB (HL)
                    OP_SUB_PR16(h, l);
                    };
                opcode[0x97] = delegate() { // SUB A
                    OP_SUB_R8(a);
                    };
                opcode[0x98] = delegate() { // SBC A, B
                    OP_SBC_R8_R8(ref a, b);
                    };
                opcode[0x99] = delegate() { // SBC A, C
                    OP_SBC_R8_R8(ref a, c);
                    };
                opcode[0x9A] = delegate() { // SBC A, D
                    OP_SBC_R8_R8(ref a, d);
                    };
                opcode[0x9B] = delegate() { // SBC A, E
                    OP_SBC_R8_R8(ref a, e);
                    };
                opcode[0x9C] = delegate() { // SBC A, H
                    OP_SBC_R8_R8(ref a, h);
                    };
                opcode[0x9D] = delegate() { // SBC A, L
                    OP_SBC_R8_R8(ref a, l);
                    };
                opcode[0x9E] = delegate() { // SBC A, (HL)
                    OP_SBC_R8_PR16(ref a, h, l);
                    };
                opcode[0x9F] = delegate() { // SBC A, A
                    OP_SBC_R8_R8(ref a, a);
                    };
                opcode[0xA0] = delegate() { // AND B
                    OP_AND_R8(b);
                    };
                opcode[0xA1] = delegate() { // AND C
                    OP_AND_R8(c);
                    };
                opcode[0xA2] = delegate() { // AND D
                    OP_AND_R8(d);
                    };
                opcode[0xA3] = delegate() { // AND E
                    OP_AND_R8(e);
                    };
                opcode[0xA4] = delegate() { // AND H
                    OP_AND_R8(h);
                    };
                opcode[0xA5] = delegate() { // AND L
                    OP_AND_R8(l);
                    };
                opcode[0xA6] = delegate() { // AND B
                    OP_AND_PR16(h, l);
                    };
                opcode[0xA7] = delegate() { // AND A
                    OP_AND_R8(a);
                    };
                opcode[0xA8] = delegate() { // XOR B
                    OP_XOR_R8(b);
                    };
                opcode[0xA9] = delegate() { // XOR C
                    OP_XOR_R8(c);
                    };
                opcode[0xAA] = delegate() { // XOR D
                    OP_XOR_R8(d);
                    };
                opcode[0xAB] = delegate() { // XOR E
                    OP_XOR_R8(e);
                    };
                opcode[0xAC] = delegate() { // XOR H
                    OP_XOR_R8(h);
                    };
                opcode[0xAD] = delegate() { // XOR L
                    OP_XOR_R8(l);
                    };
                opcode[0xAE] = delegate() { // XOR (HL)
                    OP_XOR_PR16(h, l);
                    };
                opcode[0xAF] = delegate() { // XOR A
                    OP_XOR_R8(a);
                    };
                opcode[0xB0] = delegate() { // OR B
                    OP_OR_R8(b);
                    };
                opcode[0xB1] = delegate() { // OR C
                    OP_OR_R8(c);
                    };
                opcode[0xB2] = delegate() { // OR D
                    OP_OR_R8(d);
                    };
                opcode[0xB3] = delegate() { // OR E
                    OP_OR_R8(e);
                    };
                opcode[0xB4] = delegate() { // OR H
                    OP_OR_R8(h);
                    };
                opcode[0xB5] = delegate() { // OR L
                    OP_OR_R8(l);
                    };
                opcode[0xB6] = delegate() { // OR (HL)
                    OP_OR_PR16(h, l);
                    };
                opcode[0xB7] = delegate() { // OR A
                    OP_OR_R8(a);
                    };
                opcode[0xB8] = delegate() { // CP B
                    OP_CP_R8(b);
                    };
                opcode[0xB9] = delegate() { // CP C
                    OP_CP_R8(c);
                    };
                opcode[0xBA] = delegate() { // CP D
                    OP_CP_R8(d);
                    };
                opcode[0xBB] = delegate() { // CP E
                    OP_CP_R8(e);
                    };
                opcode[0xBC] = delegate() { // CP H
                    OP_CP_R8(h);
                    };
                opcode[0xBD] = delegate() { // CP L
                    OP_CP_R8(l);
                    };
                opcode[0xBE] = delegate() { // CP (HL)
                    OP_CP_PR16(h, l);
                    };
                opcode[0xBF] = delegate() { // CP A
                    OP_CP_R8(a);
                    };
                opcode[0xC0] = delegate() { // RET NZ
                    OP_RETNZ();
                    };
                opcode[0xC1] = delegate() { // POP BC
                    OP_POP(ref b, ref c);
                    };
                opcode[0xC2] = delegate() { // JP NZ, A16
                    OP_JPNZ_A16();
                    };
                opcode[0xC3] = delegate() { // JP A16
                    OP_JP_A16();
                    };
                opcode[0xC4] = delegate() { // CALL NZ, A16
                    OP_CALLNZ_A16();
                    };
                opcode[0xC5] = delegate() { // PUSH BC
                    OP_PUSH(b, c);
                    };
                opcode[0xC6] = delegate() { // ADD A, D8
                    OP_ADD_R8_D8(ref a);
                    };
                opcode[0xC7] = delegate() { // RST 0x00
                    OP_RST(0x00);
                    };
                opcode[0xC8] = delegate() { // RET Z
                    OP_RETZ();
                    };
                opcode[0xC9] = delegate() { // RET
                    OP_RET();
                    };
                opcode[0xCA] = delegate() { // JP Z, A16
                    OP_JPZ_A16();
                    };
                opcode[0xCB] = delegate() { // PREFIX CB
                    pc++;
                    OP_EXTENDED();
                    };
                opcode[0xCC] = delegate() { // CALL Z, A16
                    OP_CALLZ_A16();
                    };
                opcode[0xCD] = delegate() { // CALL A16
                    OP_CALL_A16();
                    };
                opcode[0xCE] = delegate() { // ADC A, D8
                    OP_ADC_R8_D8(ref a);
                    };
                opcode[0xCF] = delegate() { // RST 0x08
                    OP_RST(0x08);
                    };
                opcode[0xD0] = delegate() { // RET NC
                    OP_RETNC();
                    };
                opcode[0xD1] = delegate() { // POP DE
                    OP_POP(ref d, ref e);
                    };
                opcode[0xD2] = delegate() { // JP NC, A16
                    OP_JPNC_A16();
                    };
                opcode[0xD3] = delegate() { // UNKNOWN OPCODE
                    pc++;
                    };
                opcode[0xD4] = delegate() { // CALL NC, A16
                    OP_CALLNC_A16();
                    };
                opcode[0xD5] = delegate() { // PUSH DE
                    OP_PUSH(d, e);
                    };
                opcode[0xD6] = delegate() { // SUB D8
                    OP_SUB_D8();
                    };
                opcode[0xD7] = delegate() { // RST 0x10
                    OP_RST(0x10);
                    };
                opcode[0xD8] = delegate() { // RET C
                    OP_RETC();
                    };
                opcode[0xD9] = delegate() { // RETI
                    OP_RETI();
                    };
                opcode[0xDA] = delegate() { // JP C, A16
                    OP_JPC_A16();
                    };
                opcode[0xDB] = delegate() { // UNKNOWN OPCODE
                    pc++;
                    };
                opcode[0xDC] = delegate() { // CALL C, A16
                    OP_CALLC_A16();
                    };
                opcode[0xDD] = delegate() { // UNKNOWN OPCODE
                    pc++;
                    };
                opcode[0xDE] = delegate() { // SBC A, D8
                    OP_SBC_R8_D8(ref a);
                    };
                opcode[0xDF] = delegate() { // RST 0x18
                    OP_RST(0x18);
                    };
                opcode[0xE0] = delegate() { // LD (A8), A
                    OP_LD_A8_R8(a);
                    };
                opcode[0xE1] = delegate() { // POP HL
                    OP_POP(ref h, ref l);
                    };
                opcode[0xE2] = delegate() { // LD (C), A
                    OP_LD_PR8_R8(c, a);
                    };
                opcode[0xE3] = delegate() { // UNKNOWN OPCODE
                    pc++;
                    };
                opcode[0xE4] = delegate() { // UNKNOWN OPCODE
                    pc++;
                    };
                opcode[0xE5] = delegate() { // PUSH HL
                    OP_PUSH(h, l);
                    };
                opcode[0xE6] = delegate() { // AND D8
                    OP_AND_D8();
                    };
                opcode[0xE7] = delegate() { // RST 0x20
                    OP_RST(0x20);
                    };
                opcode[0xE8] = delegate() { // ADD SP, S8
                    OP_ADD_SP_S8();
                    };
                opcode[0xE9] = delegate() { // JP HL
                    OP_JP_R16(h, l);
                    };
                opcode[0xEA] = delegate() { // LD (A16), A
                    OP_LD_A16_R8(a);
                    };
                opcode[0xEB] = delegate() { // UNKNOWN OPCODE
                    pc++;
                    };
                opcode[0xEC] = delegate() { // UNKNOWN OPCODE
                    pc++;
                    };
                opcode[0xED] = delegate() { // UNKNOWN OPCODE
                    pc++;
                    };
                opcode[0xEE] = delegate() { // XOR D8
                    OP_XOR_D8();
                    };
                opcode[0xEF] = delegate() { // RST 0x28
                    OP_RST(0x28);
                    };
                opcode[0xF0] = delegate() { // LD A, (A8)
                    OP_LD_R8_A8(ref a);
                    };
                opcode[0xF1] = delegate() { // POP AF
                    OP_POP(ref a, ref f);
                    wroteflagreg = true;
                    };
                opcode[0xF2] = delegate() { // LD A, (C)
                    OP_LD_R8_PR8(ref a, c);
                    };
                opcode[0xF3] = delegate() { // DI
                    OP_DI();
                    };
                opcode[0xF4] = delegate() { // UNKNOWN OPCODE
                    pc++;
                    };
                opcode[0xF5] = delegate() { // PUSH AF
                    OP_PUSH(a, f);
                    };
                opcode[0xF6] = delegate() { // OR D8
                    OP_OR_D8();
                    };
                opcode[0xF7] = delegate() { // RST 0x30
                    OP_RST(0x30);
                    };
                opcode[0xF8] = delegate() { // LD HL, SP+S8
                    OP_LD_R16_SP_S8(ref h, ref l);
                    };
                opcode[0xF9] = delegate() { // LD SP, HL
                    OP_LD_SP_R16(h, l);
                    };
                opcode[0xFA] = delegate() { // LD A, (A16)
                    OP_LD_R8_A16(ref a);
                    };
                opcode[0xFB] = delegate() { // EI
                    OP_EI();
                    };
                opcode[0xFC] = delegate() { // UNKNOWN OPCODE
                    pc++;
                    };
                opcode[0xFD] = delegate() { // UNKNOWN OPCODE
                    pc++;
                    };
                opcode[0xFE] = delegate() { // CP D8
                    OP_CP_D8();
                    };
                opcode[0xFF] = delegate() { // RST 0x38
                    OP_RST(0x38);
                    };
            #endregion
            #region Opcode CB table filling
                opcode2[0x00] = delegate() { // RLC B
                    OP_RLC_R8(ref b);
                    };
                opcode2[0x01] = delegate() { // RLC C
                    OP_RLC_R8(ref c);
                    };
                opcode2[0x02] = delegate() { // RLC D
                    OP_RLC_R8(ref d);
                    };
                opcode2[0x03] = delegate() { // RLC E
                    OP_RLC_R8(ref e);
                    };
                opcode2[0x04] = delegate() { // RLC H
                    OP_RLC_R8(ref h);
                    };
                opcode2[0x05] = delegate() { // RLC L
                    OP_RLC_R8(ref l);
                    };
                opcode2[0x06] = delegate() { // RLC (HL)
                    OP_RLC_PR16(h, l);
                    };
                opcode2[0x07] = delegate() { // RLC A
                    OP_RLC_R8(ref a);
                    };
                opcode2[0x08] = delegate() { // RRC B
                    OP_RRC_R8(ref b);
                    };
                opcode2[0x09] = delegate() { // RRC C
                    OP_RRC_R8(ref c);
                    };
                opcode2[0x0A] = delegate() { // RRC D
                    OP_RRC_R8(ref d);
                    };
                opcode2[0x0B] = delegate() { // RRC E
                    OP_RRC_R8(ref e);
                    };
                opcode2[0x0C] = delegate() { // RRC H
                    OP_RRC_R8(ref h);
                    };
                opcode2[0x0D] = delegate() { // RRC L
                    OP_RRC_R8(ref l);
                    };
                opcode2[0x0E] = delegate() { // RRC (HL)
                    OP_RRC_PR16(h, l);
                    };
                opcode2[0x0F] = delegate() { // RRC A
                    OP_RRC_R8(ref a);
                    };
                opcode2[0x10] = delegate() { // RL B
                    OP_RL_R8(ref b);
                    };
                opcode2[0x11] = delegate() { // RL C
                    OP_RL_R8(ref c);
                    };
                opcode2[0x12] = delegate() { // RL D
                    OP_RL_R8(ref d);
                    };
                opcode2[0x13] = delegate() { // RL E
                    OP_RL_R8(ref e);
                    };
                opcode2[0x14] = delegate() { // RL H
                    OP_RL_R8(ref h);
                    };
                opcode2[0x15] = delegate() { // RL L
                    OP_RL_R8(ref l);
                    };
                opcode2[0x16] = delegate() { // RL (HL)
                    OP_RL_PR16(h, l);
                    };
                opcode2[0x17] = delegate() { // RL A
                    OP_RL_R8(ref a);
                    };
                opcode2[0x18] = delegate() { // RR B
                    OP_RR_R8(ref b);
                    };
                opcode2[0x19] = delegate() { // RR C
                    OP_RR_R8(ref c);
                    };
                opcode2[0x1A] = delegate() { // RR D
                    OP_RR_R8(ref d);
                    };
                opcode2[0x1B] = delegate() { // RR E
                    OP_RR_R8(ref e);
                    };
                opcode2[0x1C] = delegate() { // RR H
                    OP_RR_R8(ref h);
                    };
                opcode2[0x1D] = delegate() { // RR L
                    OP_RR_R8(ref l);
                    };
                opcode2[0x1E] = delegate() { // RR (HL)
                    OP_RR_PR16(h, l);
                    };
                opcode2[0x1F] = delegate() { // RR A
                    OP_RR_R8(ref a);
                    };
                opcode2[0x20] = delegate() { // SLA B
                    OP_SLA_R8(ref b);
                    };
                opcode2[0x21] = delegate() { // SLA C
                    OP_SLA_R8(ref c);
                    };
                opcode2[0x22] = delegate() { // SLA D
                    OP_SLA_R8(ref d);
                    };
                opcode2[0x23] = delegate() { // SLA E
                    OP_SLA_R8(ref e);
                    };
                opcode2[0x24] = delegate() { // SLA H
                    OP_SLA_R8(ref h);
                    };
                opcode2[0x25] = delegate() { // SLA L
                    OP_SLA_R8(ref l);
                    };
                opcode2[0x26] = delegate() { // SLA (HL)
                    OP_SLA_PR16(h, l);
                    };
                opcode2[0x27] = delegate() { // SLA A
                    OP_SLA_R8(ref a);
                    };
                opcode2[0x28] = delegate() { // SRA B
                    OP_SRA_R8(ref b);
                    };
                opcode2[0x29] = delegate() { // SRA C
                    OP_SRA_R8(ref c);
                    };
                opcode2[0x2A] = delegate() { // SRA D
                    OP_SRA_R8(ref d);
                    };
                opcode2[0x2B] = delegate() { // SRA E
                    OP_SRA_R8(ref e);
                    };
                opcode2[0x2C] = delegate() { // SRA H
                    OP_SRA_R8(ref h);
                    };
                opcode2[0x2D] = delegate() { // SRA L
                    OP_SRA_R8(ref l);
                    };
                opcode2[0x2E] = delegate() { // SRA (HL)
                    OP_SRA_PR16(h, l);
                    };
                opcode2[0x2F] = delegate() { // SRA A
                    OP_SRA_R8(ref a);
                    };
                opcode2[0x30] = delegate() { // SWAP B
                    OP_SWAP_R8(ref b);
                    };
                opcode2[0x31] = delegate() { // SWAP C
                    OP_SWAP_R8(ref c);
                    };
                opcode2[0x32] = delegate() { // SWAP D
                    OP_SWAP_R8(ref d);
                    };
                opcode2[0x33] = delegate() { // SWAP E
                    OP_SWAP_R8(ref e);
                    };
                opcode2[0x34] = delegate() { // SWAP H
                    OP_SWAP_R8(ref h);
                    };
                opcode2[0x35] = delegate() { // SWAP L
                    OP_SWAP_R8(ref l);
                    };
                opcode2[0x36] = delegate() { // SWAP (HL)
                    OP_SWAP_PR16(h, l);
                    };
                opcode2[0x37] = delegate() { // SWAP A
                    OP_SWAP_R8(ref a);
                    };
                opcode2[0x38] = delegate() { // SRL B
                    OP_SRL_R8(ref b);
                    };
                opcode2[0x39] = delegate() { // SRL C
                    OP_SRL_R8(ref c);
                    };
                opcode2[0x3A] = delegate() { // SRL D
                    OP_SRL_R8(ref d);
                    };
                opcode2[0x3B] = delegate() { // SRL E
                    OP_SRL_R8(ref e);
                    };
                opcode2[0x3C] = delegate() { // SRL H
                    OP_SRL_R8(ref h);
                    };
                opcode2[0x3D] = delegate() { // SRL L
                    OP_SRL_R8(ref l);
                    };
                opcode2[0x3E] = delegate() { // SRL (HL)
                    OP_SRL_PR16(h, l);
                    };
                opcode2[0x3F] = delegate() { // SRL A
                    OP_SRL_R8(ref a);
                    };
                opcode2[0x40] = delegate() { // BIT 0, B
                    OP_BIT_R8(0, b);
                    };
                opcode2[0x41] = delegate() { // BIT 0, C
                    OP_BIT_R8(0, c);
                    };
                opcode2[0x42] = delegate() { // BIT 0, D
                    OP_BIT_R8(0, d);
                    };
                opcode2[0x43] = delegate() { // BIT 0, E
                    OP_BIT_R8(0, e);
                    };
                opcode2[0x44] = delegate() { // BIT 0, H
                    OP_BIT_R8(0, h);
                    };
                opcode2[0x45] = delegate() { // BIT 0, L
                    OP_BIT_R8(0, l);
                    };
                opcode2[0x46] = delegate() { // BIT 0, (HL)
                    OP_BIT_PR16(0, h, l);
                    };
                opcode2[0x47] = delegate() { // BIT 0, A
                    OP_BIT_R8(0, a);
                    };
                opcode2[0x48] = delegate() { // BIT 1, B
                    OP_BIT_R8(1, b);
                    };
                opcode2[0x49] = delegate() { // BIT 1, C
                    OP_BIT_R8(1, c);
                    };
                opcode2[0x4A] = delegate() { // BIT 1, D
                    OP_BIT_R8(1, d);
                    };
                opcode2[0x4B] = delegate() { // BIT 1, E
                    OP_BIT_R8(1, e);
                    };
                opcode2[0x4C] = delegate() { // BIT 1, H
                    OP_BIT_R8(1, h);
                    };
                opcode2[0x4D] = delegate() { // BIT 1, L
                    OP_BIT_R8(1, l);
                    };
                opcode2[0x4E] = delegate() { // BIT 1, (HL)
                    OP_BIT_PR16(1, h, l);
                    };
                opcode2[0x4F] = delegate() { // BIT 1, A
                    OP_BIT_R8(1, a);
                    };
                opcode2[0x50] = delegate() { // BIT 2, B
                    OP_BIT_R8(2, b);
                    };
                opcode2[0x51] = delegate() { // BIT 2, C
                    OP_BIT_R8(2, c);
                    };
                opcode2[0x52] = delegate() { // BIT 2, D
                    OP_BIT_R8(2, d);
                    };
                opcode2[0x53] = delegate() { // BIT 2, E
                    OP_BIT_R8(2, e);
                    };
                opcode2[0x54] = delegate() { // BIT 2, H
                    OP_BIT_R8(2, h);
                    };
                opcode2[0x55] = delegate() { // BIT 2, L
                    OP_BIT_R8(2, l);
                    };
                opcode2[0x56] = delegate() { // BIT 2, (HL)
                    OP_BIT_PR16(2, h, l);
                    };
                opcode2[0x57] = delegate() { // BIT 2, A
                    OP_BIT_R8(2, a);
                    };
                opcode2[0x58] = delegate() { // BIT 3, B
                    OP_BIT_R8(3, b);
                    };
                opcode2[0x59] = delegate() { // BIT 3, C
                    OP_BIT_R8(3, c);
                    };
                opcode2[0x5A] = delegate() { // BIT 3, D
                    OP_BIT_R8(3, d);
                    };
                opcode2[0x5B] = delegate() { // BIT 3, E
                    OP_BIT_R8(3, e);
                    };
                opcode2[0x5C] = delegate() { // BIT 3, H
                    OP_BIT_R8(3, h);
                    };
                opcode2[0x5D] = delegate() { // BIT 3, L
                    OP_BIT_R8(3, l);
                    };
                opcode2[0x5E] = delegate() { // BIT 3, (HL)
                    OP_BIT_PR16(3, h, l);
                    };
                opcode2[0x5F] = delegate() { // BIT 3, A
                    OP_BIT_R8(3, a);
                    };
                opcode2[0x60] = delegate() { // BIT 4, B
                    OP_BIT_R8(4, b);
                    };
                opcode2[0x61] = delegate() { // BIT 4, C
                    OP_BIT_R8(4, c);
                    };
                opcode2[0x62] = delegate() { // BIT 4, D
                    OP_BIT_R8(4, d);
                    };
                opcode2[0x63] = delegate() { // BIT 4, E
                    OP_BIT_R8(4, e);
                    };
                opcode2[0x64] = delegate() { // BIT 4, H
                    OP_BIT_R8(4, h);
                    };
                opcode2[0x65] = delegate() { // BIT 4, L
                    OP_BIT_R8(4, l);
                    };
                opcode2[0x66] = delegate() { // BIT 4, (HL)
                    OP_BIT_PR16(4, h, l);
                    };
                opcode2[0x67] = delegate() { // BIT 4, A
                    OP_BIT_R8(4, a);
                    };
                opcode2[0x68] = delegate() { // BIT 5, B
                    OP_BIT_R8(5, b);
                    };
                opcode2[0x69] = delegate() { // BIT 5, C
                    OP_BIT_R8(5, c);
                    };
                opcode2[0x6A] = delegate() { // BIT 5, D
                    OP_BIT_R8(5, d);
                    };
                opcode2[0x6B] = delegate() { // BIT 5, E
                    OP_BIT_R8(5, e);
                    };
                opcode2[0x6C] = delegate() { // BIT 5, H
                    OP_BIT_R8(5, h);
                    };
                opcode2[0x6D] = delegate() { // BIT 5, L
                    OP_BIT_R8(5, l);
                    };
                opcode2[0x6E] = delegate() { // BIT 5, (HL)
                    OP_BIT_PR16(5, h, l);
                    };
                opcode2[0x6F] = delegate() { // BIT 5, A
                    OP_BIT_R8(5, a);
                    };
                opcode2[0x70] = delegate() { // BIT 6, B
                    OP_BIT_R8(6, b);
                    };
                opcode2[0x71] = delegate() { // BIT 6, C
                    OP_BIT_R8(6, c);
                    };
                opcode2[0x72] = delegate() { // BIT 6, D
                    OP_BIT_R8(6, d);
                    };
                opcode2[0x73] = delegate() { // BIT 6, E
                    OP_BIT_R8(6, e);
                    };
                opcode2[0x74] = delegate() { // BIT 6, H
                    OP_BIT_R8(6, h);
                    };
                opcode2[0x75] = delegate() { // BIT 6, L
                    OP_BIT_R8(6, l);
                    };
                opcode2[0x76] = delegate() { // BIT 6, (HL)
                    OP_BIT_PR16(6, h, l);
                    };
                opcode2[0x77] = delegate() { // BIT 6, A
                    OP_BIT_R8(6, a);
                    };
                opcode2[0x78] = delegate() { // BIT 7, B
                    OP_BIT_R8(7, b);
                    };
                opcode2[0x79] = delegate() { // BIT 7, C
                    OP_BIT_R8(7, c);
                    };
                opcode2[0x7A] = delegate() { // BIT 7, D
                    OP_BIT_R8(7, d);
                    };
                opcode2[0x7B] = delegate() { // BIT 7, E
                    OP_BIT_R8(7, e);
                    };
                opcode2[0x7C] = delegate() { // BIT 7, H
                    OP_BIT_R8(7, h);
                    };
                opcode2[0x7D] = delegate() { // BIT 7, L
                    OP_BIT_R8(7, l);
                    };
                opcode2[0x7E] = delegate() { // BIT 7, (HL)
                    OP_BIT_PR16(7, h, l);
                    };
                opcode2[0x7F] = delegate() { // BIT 7, A
                    OP_BIT_R8(7, a);
                    };
                opcode2[0x80] = delegate() { // RES 0, B
                    OP_RES_R8(0, ref b);
                    };
                opcode2[0x81] = delegate() { // RES 0, C
                    OP_RES_R8(0, ref c);
                    };
                opcode2[0x82] = delegate() { // RES 0, D
                    OP_RES_R8(0, ref d);
                    };
                opcode2[0x83] = delegate() { // RES 0, E
                    OP_RES_R8(0, ref e);
                    };
                opcode2[0x84] = delegate() { // RES 0, H
                    OP_RES_R8(0, ref h);
                    };
                opcode2[0x85] = delegate() { // RES 0, L
                    OP_RES_R8(0, ref l);
                    };
                opcode2[0x86] = delegate() { // RES 0, (HL)
                    OP_RES_PR16(0, h, l);
                    };
                opcode2[0x87] = delegate() { // RES 0, A
                    OP_RES_R8(0, ref a);
                    };
                opcode2[0x88] = delegate() { // RES 1, B
                    OP_RES_R8(1, ref b);
                    };
                opcode2[0x89] = delegate() { // RES 1, C
                    OP_RES_R8(1, ref c);
                    };
                opcode2[0x8A] = delegate() { // RES 1, D
                    OP_RES_R8(1, ref d);
                    };
                opcode2[0x8B] = delegate() { // RES 1, E
                    OP_RES_R8(1, ref e);
                    };
                opcode2[0x8C] = delegate() { // RES 1, H
                    OP_RES_R8(1, ref h);
                    };
                opcode2[0x8D] = delegate() { // RES 1, L
                    OP_RES_R8(1, ref l);
                    };
                opcode2[0x8E] = delegate() { // RES 1, (HL)
                    OP_RES_PR16(1, h, l);
                    };
                opcode2[0x8F] = delegate() { // RES 1, A
                    OP_RES_R8(1, ref a);
                    };
                opcode2[0x90] = delegate() { // RES 2, B
                    OP_RES_R8(2, ref b);
                    };
                opcode2[0x91] = delegate() { // RES 2, C
                    OP_RES_R8(2, ref c);
                    };
                opcode2[0x92] = delegate() { // RES 2, D
                    OP_RES_R8(2, ref d);
                    };
                opcode2[0x93] = delegate() { // RES 2, E
                    OP_RES_R8(2, ref e);
                    };
                opcode2[0x94] = delegate() { // RES 2, H
                    OP_RES_R8(2, ref h);
                    };
                opcode2[0x95] = delegate() { // RES 2, L
                    OP_RES_R8(2, ref l);
                    };
                opcode2[0x96] = delegate() { // RES 2, (HL)
                    OP_RES_PR16(2, h, l);
                    };
                opcode2[0x97] = delegate() { // RES 2, A
                    OP_RES_R8(2, ref a);
                    };
                opcode2[0x98] = delegate() { // RES 3, B
                    OP_RES_R8(3, ref b);
                    };
                opcode2[0x99] = delegate() { // RES 3, C
                    OP_RES_R8(3, ref c);
                    };
                opcode2[0x9A] = delegate() { // RES 3, D
                    OP_RES_R8(3, ref d);
                    };
                opcode2[0x9B] = delegate() { // RES 3, E
                    OP_RES_R8(3, ref e);
                    };
                opcode2[0x9C] = delegate() { // RES 3, H
                    OP_RES_R8(3, ref h);
                    };
                opcode2[0x9D] = delegate() { // RES 3, L
                    OP_RES_R8(3, ref l);
                    };
                opcode2[0x9E] = delegate() { // RES 3, (HL)
                    OP_RES_PR16(3, h, l);
                    };
                opcode2[0x9F] = delegate() { // RES 3, A
                    OP_RES_R8(3, ref a);
                    };
                opcode2[0xA0] = delegate() { // RES 4, B
                    OP_RES_R8(4, ref b);
                    };
                opcode2[0xA1] = delegate() { // RES 4, C
                    OP_RES_R8(4, ref c);
                    };
                opcode2[0xA2] = delegate() { // RES 4, D
                    OP_RES_R8(4, ref d);
                    };
                opcode2[0xA3] = delegate() { // RES 4, E
                    OP_RES_R8(4, ref e);
                    };
                opcode2[0xA4] = delegate() { // RES 4, H
                    OP_RES_R8(4, ref h);
                    };
                opcode2[0xA5] = delegate() { // RES 4, L
                    OP_RES_R8(4, ref l);
                    };
                opcode2[0xA6] = delegate() { // RES 4, (HL)
                    OP_RES_PR16(4, h, l);
                    };
                opcode2[0xA7] = delegate() { // RES 4, A
                    OP_RES_R8(4, ref a);
                    };
                opcode2[0xA8] = delegate() { // RES 5, B
                    OP_RES_R8(5, ref b);
                    };
                opcode2[0xA9] = delegate() { // RES 5, C
                    OP_RES_R8(5, ref c);
                    };
                opcode2[0xAA] = delegate() { // RES 5, D
                    OP_RES_R8(5, ref d);
                    };
                opcode2[0xAB] = delegate() { // RES 5, E
                    OP_RES_R8(5, ref e);
                    };
                opcode2[0xAC] = delegate() { // RES 5, H
                    OP_RES_R8(5, ref h);
                    };
                opcode2[0xAD] = delegate() { // RES 5, L
                    OP_RES_R8(5, ref l);
                    };
                opcode2[0xAE] = delegate() { // RES 5, (HL)
                    OP_RES_PR16(5, h, l);
                    };
                opcode2[0xAF] = delegate() { // RES 5, A
                    OP_RES_R8(5, ref a);
                    };
                opcode2[0xB0] = delegate() { // RES 6, B
                    OP_RES_R8(6, ref b);
                    };
                opcode2[0xB1] = delegate() { // RES 6, C
                    OP_RES_R8(6, ref c);
                    };
                opcode2[0xB2] = delegate() { // RES 6, D
                    OP_RES_R8(6, ref d);
                    };
                opcode2[0xB3] = delegate() { // RES 6, E
                    OP_RES_R8(6, ref e);
                    };
                opcode2[0xB4] = delegate() { // RES 6, H
                    OP_RES_R8(6, ref h);
                    };
                opcode2[0xB5] = delegate() { // RES 6, L
                    OP_RES_R8(6, ref l);
                    };
                opcode2[0xB6] = delegate() { // RES 6, (HL)
                    OP_RES_PR16(6, h, l);
                    };
                opcode2[0xB7] = delegate() { // RES 6, A
                    OP_RES_R8(6, ref a);
                    };
                opcode2[0xB8] = delegate() { // RES 7, B
                    OP_RES_R8(7, ref b);
                    };
                opcode2[0xB9] = delegate() { // RES 7, C
                    OP_RES_R8(7, ref c);
                    };
                opcode2[0xBA] = delegate() { // RES 7, D
                    OP_RES_R8(7, ref d);
                    };
                opcode2[0xBB] = delegate() { // RES 7, E
                    OP_RES_R8(7, ref e);
                    };
                opcode2[0xBC] = delegate() { // RES 7, H
                    OP_RES_R8(7, ref h);
                    };
                opcode2[0xBD] = delegate() { // RES 7, L
                    OP_RES_R8(7, ref l);
                    };
                opcode2[0xBE] = delegate() { // RES 7, (HL)
                    OP_RES_PR16(7, h, l);
                    };
                opcode2[0xBF] = delegate() { // RES 7, A
                    OP_RES_R8(7, ref a);
                    };
                opcode2[0xC0] = delegate() { // SET 0, B
                    OP_SET_R8(0, ref b);
                    };
                opcode2[0xC1] = delegate() { // SET 0, C
                    OP_SET_R8(0, ref c);
                    };
                opcode2[0xC2] = delegate() { // SET 0, D
                    OP_SET_R8(0, ref d);
                    };
                opcode2[0xC3] = delegate() { // SET 0, E
                    OP_SET_R8(0, ref e);
                    };
                opcode2[0xC4] = delegate() { // SET 0, H
                    OP_SET_R8(0, ref h);
                    };
                opcode2[0xC5] = delegate() { // SET 0, L
                    OP_SET_R8(0, ref l);
                    };
                opcode2[0xC6] = delegate() { // SET 0, (HL)
                    OP_SET_PR16(0, h, l);
                    };
                opcode2[0xC7] = delegate() { // SET 0, A
                    OP_SET_R8(0, ref a);
                    };
                opcode2[0xC8] = delegate() { // SET 1, B
                    OP_SET_R8(1, ref b);
                    };
                opcode2[0xC9] = delegate() { // SET 1, C
                    OP_SET_R8(1, ref c);
                    };
                opcode2[0xCA] = delegate() { // SET 1, D
                    OP_SET_R8(1, ref d);
                    };
                opcode2[0xCB] = delegate() { // SET 1, E
                    OP_SET_R8(1, ref e);
                    };
                opcode2[0xCC] = delegate() { // SET 1, H
                    OP_SET_R8(1, ref h);
                    };
                opcode2[0xCD] = delegate() { // SET 1, L
                    OP_SET_R8(1, ref l);
                    };
                opcode2[0xCE] = delegate() { // SET 1, (HL)
                    OP_SET_PR16(1, h, l);
                    };
                opcode2[0xCF] = delegate() { // SET 1, A
                    OP_SET_R8(1, ref a);
                    };
                opcode2[0xD0] = delegate() { // SET 2, B
                    OP_SET_R8(2, ref b);
                    };
                opcode2[0xD1] = delegate() { // SET 2, C
                    OP_SET_R8(2, ref c);
                    };
                opcode2[0xD2] = delegate() { // SET 2, D
                    OP_SET_R8(2, ref d);
                    };
                opcode2[0xD3] = delegate() { // SET 2, E
                    OP_SET_R8(2, ref e);
                    };
                opcode2[0xD4] = delegate() { // SET 2, H
                    OP_SET_R8(2, ref h);
                    };
                opcode2[0xD5] = delegate() { // SET 2, L
                    OP_SET_R8(2, ref l);
                    };
                opcode2[0xD6] = delegate() { // SET 2, (HL)
                    OP_SET_PR16(2, h, l);
                    };
                opcode2[0xD7] = delegate() { // SET 2, A
                    OP_SET_R8(2, ref a);
                    };
                opcode2[0xD8] = delegate() { // SET 3, B
                    OP_SET_R8(3, ref b);
                    };
                opcode2[0xD9] = delegate() { // SET 3, C
                    OP_SET_R8(3, ref c);
                    };
                opcode2[0xDA] = delegate() { // SET 3, D
                    OP_SET_R8(3, ref d);
                    };
                opcode2[0xDB] = delegate() { // SET 3, E
                    OP_SET_R8(3, ref e);
                    };
                opcode2[0xDC] = delegate() { // SET 3, H
                    OP_SET_R8(3, ref h);
                    };
                opcode2[0xDD] = delegate() { // SET 3, L
                    OP_SET_R8(3, ref l);
                    };
                opcode2[0xDE] = delegate() { // SET 3, (HL)
                    OP_SET_PR16(3, h, l);
                    };
                opcode2[0xDF] = delegate() { // SET 3, A
                    OP_SET_R8(3, ref a);
                    };
                opcode2[0xE0] = delegate() { // SET 4, B
                    OP_SET_R8(4, ref b);
                    };
                opcode2[0xE1] = delegate() { // SET 4, C
                    OP_SET_R8(4, ref c);
                    };
                opcode2[0xE2] = delegate() { // SET 4, D
                    OP_SET_R8(4, ref d);
                    };
                opcode2[0xE3] = delegate() { // SET 4, E
                    OP_SET_R8(4, ref e);
                    };
                opcode2[0xE4] = delegate() { // SET 4, H
                    OP_SET_R8(4, ref h);
                    };
                opcode2[0xE5] = delegate() { // SET 4, L
                    OP_SET_R8(4, ref l);
                    };
                opcode2[0xE6] = delegate() { // SET 4, (HL)
                    OP_SET_PR16(4, h, l);
                    };
                opcode2[0xE7] = delegate() { // SET 4, A
                    OP_SET_R8(4, ref a);
                    };
                opcode2[0xE8] = delegate() { // SET 5, B
                    OP_SET_R8(5, ref b);
                    };
                opcode2[0xE9] = delegate() { // SET 5, C
                    OP_SET_R8(5, ref c);
                    };
                opcode2[0xEA] = delegate() { // SET 5, D
                    OP_SET_R8(5, ref d);
                    };
                opcode2[0xEB] = delegate() { // SET 5, E
                    OP_SET_R8(5, ref e);
                    };
                opcode2[0xEC] = delegate() { // SET 5, H
                    OP_SET_R8(5, ref h);
                    };
                opcode2[0xED] = delegate() { // SET 5, L
                    OP_SET_R8(5, ref l);
                    };
                opcode2[0xEE] = delegate() { // SET 5, (HL)
                    OP_SET_PR16(5, h, l);
                    };
                opcode2[0xEF] = delegate() { // SET 5, A
                    OP_SET_R8(5, ref a);
                    };
                opcode2[0xF0] = delegate() { // SET 6, B
                    OP_SET_R8(6, ref b);
                    };
                opcode2[0xF1] = delegate() { // SET 6, C
                    OP_SET_R8(6, ref c);
                    };
                opcode2[0xF2] = delegate() { // SET 6, D
                    OP_SET_R8(6, ref d);
                    };
                opcode2[0xF3] = delegate() { // SET 6, E
                    OP_SET_R8(6, ref e);
                    };
                opcode2[0xF4] = delegate() { // SET 6, H
                    OP_SET_R8(6, ref h);
                    };
                opcode2[0xF5] = delegate() { // SET 6, L
                    OP_SET_R8(6, ref l);
                    };
                opcode2[0xF6] = delegate() { // SET 6, (HL)
                    OP_SET_PR16(6, h, l);
                    };
                opcode2[0xF7] = delegate() { // SET 6, A
                    OP_SET_R8(6, ref a);
                    };
                opcode2[0xF8] = delegate() { // SET 7, B
                    OP_SET_R8(7, ref b);
                    };
                opcode2[0xF9] = delegate() { // SET 7, C
                    OP_SET_R8(7, ref c);
                    };
                opcode2[0xFA] = delegate() { // SET 7, D
                    OP_SET_R8(7, ref d);
                    };
                opcode2[0xFB] = delegate() { // SET 7, E
                    OP_SET_R8(7, ref e);
                    };
                opcode2[0xFC] = delegate() { // SET 7, H
                    OP_SET_R8(7, ref h);
                    };
                opcode2[0xFD] = delegate() { // SET 7, L
                    OP_SET_R8(7, ref l);
                    };
                opcode2[0xFE] = delegate() { // SET 7, (HL)
                    OP_SET_PR16(7, h, l);
                    };
                opcode2[0xFF] = delegate() { // SET 7, A
                    OP_SET_R8(7, ref a);
                    };
            #endregion
            observers = new List<IObserver<CPUStatusUpdate>>();
        }

        public IDisposable Subscribe(IObserver<CPUStatusUpdate> observer)
        {
            observers.Add(observer);
            return new Subscription<CPUStatusUpdate>(observers, observer);
        }

        private void Notify(CPUStatusUpdate update)
        {
            for (int i = 0; i < observers.Count; i++) {
                if (observers[i] != null) {
                    observers[i].OnNext(update);
                }
            }
        }

        public int Tick()
        {
            if (!Running || WaitForInterrupt || IsStopped || IsLockedUp) {
                return 4;
            }

            pc &= 0xFFFF;
            CPUStatusUpdate update = new CPUStatusUpdate();
            update.Reason = CPUStatusUpdate.UpdateReason.Execution;
            update.Offset = pc;
            update.CPU = this;
            Notify(update);
            if (!Running || WaitForInterrupt) {
                return 4;
            }

            byte op = ReadByte(pc);
            if (!Running || WaitForInterrupt) {
                return 4;
            }

            if (haltBug) {
                pc = (pc - 1) & 0xFFFF;
                haltBug = false;
            }

            if (IsIllegalOpcode(op)) {
                pc = (pc + 1) & 0xFFFF;
                IsLockedUp = true;
                return 4;
            }

            cycleamountext = 0;
            wroteflagreg = false;
            f = (FlagZ ? 0x80 : 0) |
                (FlagN ? 0x40 : 0) |
                (FlagH ? 0x20 : 0) |
                (FlagC ? 0x10 : 0);
            branched = false;
            opcode[op]();
            if (wroteflagreg) {
                FlagZ = ((f >> 7) & 1) == 1;
                FlagN = ((f >> 6) & 1) == 1;
                FlagH = ((f >> 5) & 1) == 1;
                FlagC = ((f >> 4) & 1) == 1;
            }
            AdvanceImeEnableDelay();
            if (cycleamountext == 0) {
                return branched ? cyclesbranched[op] : cycles[op];
            } else {
                return cycleamountext;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Push(int v)
        {
            CPUStatusUpdate update = new CPUStatusUpdate();
            sp -= 2;
            WriteShort(sp, v);
            update.Reason = CPUStatusUpdate.UpdateReason.Push;
            update.Offset = sp;
            update.Value = v;
            update.CPU = this;
            Notify(update);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Pop()
        {
            CPUStatusUpdate update = new CPUStatusUpdate();
            int v = ReadShort(sp);
            update.Reason = CPUStatusUpdate.UpdateReason.Pop;
            update.Offset = sp;
            update.Value = v;
            update.CPU = this;
            Notify(update);
            sp += 2;
            return v;
        }

        public void Interrupt(int address)
        {
            WaitForInterrupt = false;
            IME = false;
            imeEnableDelay = 0;
            Push(pc);
            pc = address;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte ReadByte(int address)
        {
            CPUStatusUpdate update = new CPUStatusUpdate();
            byte value = Memory.ReadByte(address);
            update.Reason = CPUStatusUpdate.UpdateReason.MemoryRead;
            update.Offset = address;
            update.Value = value;
            update.CPU = this;
            Notify(update);
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort ReadShort(int address)
        {
            return (ushort)((Memory.ReadByte(address + 1) << 8) + Memory.ReadByte(address));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteByte(int address, int value)
        {
            CPUStatusUpdate update = new CPUStatusUpdate();
            update.Reason = CPUStatusUpdate.UpdateReason.MemoryWrite;
            update.Offset = address;
            update.Value = value;
            update.CPU = this;
            Notify(update);
            Memory.WriteByte(address, (byte)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteShort(int address, int value)
        {
            WriteByte(address, value & 0xFF);
            WriteByte(address + 1, value >> 8);
        }

        #region Opcode implementation

        #region Interrupts

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_RETI()
        {
            IME = true;
            imeEnableDelay = 0;
            pc = Pop();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_EI()
        {
            if (!IME && imeEnableDelay == 0)
            {
                imeEnableDelay = 2;
            }
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_DI()
        {
            IME = false;
            imeEnableDelay = 0;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdvanceImeEnableDelay()
        {
            if (imeEnableDelay == 0)
            {
                return;
            }

            imeEnableDelay--;
            if (imeEnableDelay == 0)
            {
                IME = true;
            }
        }

        internal void ResetExecutionState()
        {
            Running = true;
            IME = false;
            WaitForInterrupt = false;
            IsStopped = false;
            IsLockedUp = false;
            imeEnableDelay = 0;
            haltBug = false;
            PrepareSpeedSwitch = false;
            IsDoubleSpeed = false;
        }

        internal void WakeFromStop()
        {
            IsStopped = false;
        }

        private static bool IsIllegalOpcode(byte opcodeValue) =>
            opcodeValue == 0xD3 ||
            opcodeValue == 0xDB ||
            opcodeValue == 0xDD ||
            opcodeValue == 0xE3 ||
            opcodeValue == 0xE4 ||
            opcodeValue == 0xEB ||
            opcodeValue == 0xEC ||
            opcodeValue == 0xED ||
            opcodeValue == 0xF4 ||
            opcodeValue == 0xFC ||
            opcodeValue == 0xFD;

        #endregion

        #region Stack

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_PUSH(int rh, int rl)
        {
            Push((rh << 8) + rl);
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_POP(ref int rh, ref int rl)
        {
            int v = Pop();
            rh = v >> 8;
            rl = v & 0xFF;
            pc++;
        }

        #endregion

        #region Compare

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_CP_R8(int ra)
        {
            FlagN = true;
            FlagH = (a & 0xF) < (ra & 0xF);
            FlagC = ra > a;
            FlagZ = ((a - ra) & 0xFF) == 0;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_CP_PR16(int rah, int ral)
        {
            int v = ReadByte((rah << 8) + ral);
            FlagN = true;
            FlagH = (a & 0xF) < (v & 0xF);
            FlagC = v > a;
            FlagZ = ((a - v) & 0xFF) == 0;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_CP_D8()
        {
            int v = ReadByte(pc + 1);
            FlagN = true;
            FlagH = (a & 0xF) < (v & 0xF);
            FlagC = v > a;
            FlagZ = ((a - v) & 0xFF) == 0;
            pc += 2;
        }

        #endregion

        #region Program control (Jumps, etc.)

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_JR_S8()
        {
            int relAddr = ReadByte(pc + 1);
            if (relAddr > 0x7F)
            {
                relAddr -= 256;
            }
            pc += relAddr + 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_JRNZ_S8()
        {
            if (!FlagZ)
            {
                branched = true;
                OP_JR_S8();
            } else {
                pc += 2;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_JRZ_S8()
        {
            if (FlagZ)
            {
                branched = true;
                OP_JR_S8();
            } else {
                pc += 2;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_JRNC_S8()
        {
            if (!FlagC)
            {
                branched = true;
                OP_JR_S8();
            } else {
                pc += 2;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_JRC_S8()
        {
            if (FlagC)
            {
                branched = true;
                OP_JR_S8();
            } else {
                pc += 2;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_JP_A16()
        {
            pc = ReadShort(pc + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_JPNZ_A16()
        {
            if (!FlagZ)
            {
                branched = true;
                OP_JP_A16();
            } else {
                pc += 3;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_JPZ_A16()
        {
            if (FlagZ)
            {
                branched = true;
                OP_JP_A16();
            } else {
                pc += 3;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_JPNC_A16()
        {
            if (!FlagC)
            {
                branched = true;
                OP_JP_A16();
            } else {
                pc += 3;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_JPC_A16()
        {
            if (FlagC)
            {
                branched = true;
                OP_JP_A16();
            } else {
                pc += 3;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_JP_R16(int rh, int rl)
        {
            pc = (rh << 8) + rl;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_CALL_A16()
        {
            Push(pc + 3);
            pc = ReadShort(pc + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_CALLNZ_A16()
        {
            if (!FlagZ)
            {
                branched = true;
                OP_CALL_A16();
            } else {
                pc += 3;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_CALLZ_A16()
        {
            if (FlagZ)
            {
                branched = true;
                OP_CALL_A16();
            } else {
                pc += 3;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_CALLNC_A16()
        {
            if (!FlagC)
            {
                branched = true;
                OP_CALL_A16();
            } else {
                pc += 3;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_CALLC_A16()
        {
            if (FlagC)
            {
                branched = true;
                OP_CALL_A16();
            } else {
                pc += 3;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_HALT()
        {
            int pendingInterrupts = Memory.ReadByte(0xFFFF) & Memory.ReadByte(0xFF0F) & 0x1F;
            if (!IME && pendingInterrupts != 0) {
                haltBug = true;
            } else {
                WaitForInterrupt = true;
            }
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_RET()
        {
            pc = Pop();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_RETNZ()
        {
            if (!FlagZ)
            {
                branched = true;
                OP_RET();
            } else {
                pc++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_RETZ()
        {
            if (FlagZ)
            {
                branched = true;
                OP_RET();
            } else {
                pc++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_RETNC()
        {
            if (!FlagC)
            {
                branched = true;
                OP_RET();
            } else {
                pc++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_RETC()
        {
            if (FlagC)
            {
                branched = true;
                OP_RET();
            } else {
                pc++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_RST(int address)
        {
            Push(pc + 1);
            pc = address;
        }

        #endregion

        #region Load

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_LD_R8_R8(ref int ra, int rb)
        {
            ra = rb;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_LD_R16_D16(ref int rh, ref int rl)
        {
            rh = ReadByte(pc + 2);
            rl = ReadByte(pc + 1);
            pc += 3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_LD_PR16_R8(int rah, int ral, int rb)
        {
            WriteByte((rah << 8) + ral, rb);
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_LD_R8_D8(ref int r)
        {
            r = ReadByte(pc + 1);
            pc += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_LD_A16_R8(int r)
        {
            WriteByte(ReadShort(pc + 1), r);
            pc += 3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_LD_A16_R16(int rh, int rl)
        {
            WriteShort(ReadShort(pc + 1), (rh << 8) + rl);
            pc += 3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_LD_R8_A16(ref int r)
        {
            r = ReadByte(ReadShort(pc + 1));
            pc += 3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_LD_R8_PR16(ref int r, int rbh, int rbl)
        {
            r = ReadByte((rbh << 8) + rbl);
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_LD_PR16_D8(int rah, int ral)
        {
            WriteByte((rah << 8) + ral, ReadByte(pc + 1));
            pc += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_LD_SP_D16()
        {
            sp = ReadShort(pc + 1);
            pc += 3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_LD_SP_R16(int rh, int rl)
        {
            sp = (rh << 8) + rl;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_LD_R16_SP_S8(ref int rh, ref int rl)
        {
            int v = ReadByte(pc + 1);
            if (v > 0x7F)
            {
                v -= 256;
            }

            if (v >= 0)
            {
                FlagH = (sp & 0xF) + (v & 0xF) > 0xF;
                FlagC = (sp & 0xFF) + v > 0xFF;
                FlagN = false;
            } else {
                int vp = v * -1;
                FlagH = (sp & 0xF) < (vp & 0xF);
                FlagC = vp > (sp & 0xFF);
                FlagN = true;
            }

            int result = (sp + v) & 0xFFFF;
            rl = result & 0xFF;
            rh = result >> 8;

            pc += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_LDI_PR16_R8(ref int rah, ref int ral, int rb)
        {
            OP_LD_PR16_R8(rah, ral, rb);
            OP_INC_R16(ref rah, ref ral);
            pc--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_LDI_R8_PR16(ref int ra, ref int rbh, ref int rbl)
        {
            OP_LD_R8_PR16(ref ra, rbh, rbl);
            OP_INC_R16(ref rbh, ref rbl);
            pc--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_LDD_PR16_R8(ref int rah, ref int ral, int rb)
        {
            OP_LD_PR16_R8(rah, ral, rb);
            OP_DEC_R16(ref rah, ref ral);
            pc--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_LDD_R8_PR16(ref int ra, ref int rbh, ref int rbl)
        {
            OP_LD_R8_PR16(ref ra, rbh, rbl);
            OP_DEC_R16(ref rbh, ref rbl);
            pc--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_LD_A8_R8(int ra)
        {
            WriteByte(0xFF00 + ReadByte(pc + 1), ra);
            pc += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_LD_R8_A8(ref int ra)
        {
            ra = ReadByte(0xFF00 + ReadByte(pc + 1));
            pc += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_LD_PR8_R8(int ra, int rb)
        {
            WriteByte(0xFF00 + ra, rb);
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_LD_R8_PR8(ref int ra, int rb)
        {
            ra = ReadByte(0xFF00 + rb);
            pc++;
        }

        #endregion

        #region Math

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_INC_R16(ref int rh, ref int rl)
        {
            if (rl == 0xFF)
            {
                rh = (rh + 1) & 0xFF;
                rl = 0;
            } else {
                rl++;
            }
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_INC_R8(ref int r)
        {
            FlagN = false;
            FlagH = (r & 0xF) == 0xF;
            r++;
            r &= 0xFF;
            FlagZ = r == 0;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_INC_PR16(int rh, int rl)
        {
            int addr = (rh << 8) + rl;
            int vorig = ReadByte(addr);
            int v = (vorig + 1) & 0xFF;
            WriteByte(addr, v);
            FlagZ = v == 0;
            FlagN = false;
            FlagH = (vorig & 0xF) == 0xF;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_INC_SP()
        {
            if (sp == 0xFFFF)
            {
                sp = 0;
            } else {
                sp++;
            }
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_DEC_R16(ref int RH, ref int RL)
        {
            if (RL == 0)
            {
                RH = (RH - 1) & 0xFF;
                RL = 0xFF;
            } else {
                RL--;
            }
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_DEC_R8(ref int R)
        {
            FlagN = true;
            FlagH = (R & 0xF) == 0x0;
            R--;
            R &= 0xFF;
            FlagZ = R == 0;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_DEC_PR16(int rh, int rl)
        {
            int addr = (rh << 8) + rl;
            int vOrig = ReadByte(addr);
            int v = (vOrig - 1) & 0xFF;
            WriteByte(addr, v);
            FlagZ = v == 0;
            FlagN = true;
            FlagH = (vOrig & 0xF) == 0x0;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_ADD_R16_R16(ref int rah, ref int ral, int rbh, int rbl)
        {
            FlagN = false;
            ral += rbl;
            int carry = (ral > 0xFF) ? 1 : 0;
            ral &= 0xFF;
            FlagH = carry + (rah & 0xF) + (rbh & 0xF) > 0xF;
            rah += rbh + carry;
            FlagC = rah > 0xFF;
            rah &= 0xFF;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_ADD_R8_R8(ref int ra, int rb)
        {
            FlagN = false;
            FlagH = (ra & 0xF) + (rb & 0xF) > 0xF;
            ra += rb;
            FlagC = ra > 0xFF;
            ra &= 0xFF;
            FlagZ = ra == 0;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_ADD_R8_PR16(ref int ra, int rbh, int rbl)
        {
            int v = ReadByte((rbh << 8) + rbl);
            FlagN = false;
            FlagH = (ra & 0xF) + (v & 0xF) > 0xF;
            ra += v;
            FlagC = ra > 0xFF;
            ra &= 0xFF;
            FlagZ = ra == 0;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_ADD_R8_D8(ref int ra)
        {
            int v = ReadByte(pc + 1);
            FlagN = false;
            FlagH = (ra & 0xF) + (v & 0xF) > 0xF;
            ra += v;
            FlagC = ra > 0xFF;
            ra &= 0xFF;
            FlagZ = ra == 0;
            pc += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_ADD_SP_S8()
        {
            int v = ReadByte(pc + 1);
            if (v > 0x7F)
            {
                v -= 256;
            }
            FlagZ = (sp + v) == 0;
            FlagN = v < 0;
            FlagH = (sp & 0xF) + (v & 0xF) > 0xF;
            FlagC = (sp & 0xFF) + v > 0xFF;
            sp += v;
            sp &= 0xFFFF;
            pc += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_ADC_R8_R8(ref int ra, int rb)
        {
            int carryBit = FlagC ? 1 : 0;
            FlagN = false;
            FlagH = (ra & 0xF) + (rb & 0xF) + carryBit > 0xF;
            ra += rb + carryBit;
            FlagC = ra > 0xFF;
            ra &= 0xFF;
            FlagZ = ra == 0;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_ADC_R8_PR16(ref int ra, int rbh, int rbl)
        {
            int v = ReadByte((rbh << 8) + rbl);
            int carryBit = FlagC ? 1 : 0;
            FlagN = false;
            FlagH = (ra & 0xF) + (v & 0xF) + carryBit > 0xF;
            ra += v + carryBit;
            FlagC = ra > 0xFF;
            ra &= 0xFF;
            FlagZ = ra == 0;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_ADC_R8_D8(ref int ra)
        {
            int v = ReadByte(pc + 1);
            int carryBit = FlagC ? 1 : 0;
            FlagN = false;
            FlagH = (ra & 0xF) + (v & 0xF) + carryBit > 0xF;
            ra += v + carryBit;
            FlagC = ra > 0xFF;
            ra &= 0xFF;
            FlagZ = ra == 0;
            pc += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_SUB_R8(int ra)
        {
            FlagN = true;
            FlagH = (a & 0xF) < (ra & 0xF);
            FlagC = ra > a;
            a -= ra;
            a &= 0xFF;
            FlagZ = a == 0;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_SUB_PR16(int rah, int ral)
        {
            int v = ReadByte((rah << 8) + ral);
            FlagN = true;
            FlagH = (a & 0xF) < (v & 0xF);
            FlagC = v > a;
            a -= v;
            a &= 0xFF;
            FlagZ = a == 0;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_SUB_D8()
        {
            int v = ReadByte(pc + 1);
            FlagN = true;
            FlagH = (a & 0xF) < (v & 0xF);
            FlagC = v > a;
            a -= v;
            a &= 0xFF;
            FlagZ = a == 0;
            pc += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_SBC_R8_R8(ref int ra, int rb)
        {
            int carryBit = FlagC ? 1 : 0;
            FlagN = true;
            FlagH = (ra & 0xF) < (rb & 0xF) + carryBit;
            FlagC = rb + carryBit > ra;
            ra -= rb + carryBit;
            ra &= 0xFF;
            FlagZ = ra == 0;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_SBC_R8_PR16(ref int ra, int rah, int ral)
        {
            int carryBit = FlagC ? 1 : 0;
            int v = ReadByte((rah << 8) + ral);
            FlagN = true;
            FlagH = (ra & 0xF) < ((v & 0xF) + carryBit);
            FlagC = v + carryBit > ra;
            ra -= v + carryBit;
            ra &= 0xFF;
            FlagZ = ra == 0;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_SBC_R8_D8(ref int ra)
        {
            int carryBit = FlagC ? 1 : 0;
            int v = ReadByte(pc + 1);
            FlagN = true;
            FlagH = (ra & 0xF) < (v & 0xF) + carryBit;
            FlagC = v + carryBit > ra;
            ra -= v + carryBit;
            ra &= 0xFF;
            FlagZ = ra == 0;
            pc += 2;
        }

        #endregion

        #region Bit operations

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_RLCA()
        {
            int highBit = a >> 7;
            FlagZ = false;
            FlagN = false;
            FlagH = false;
            FlagC = highBit == 1;
            a = ((a << 1) & 0xFF) | highBit;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_RRCA()
        {
            int lowBit = a & 1;
            FlagZ = false;
            FlagN = false;
            FlagH = false;
            FlagC = lowBit == 1;
            a = (a >> 1) | (lowBit << 7);
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_RLA()
        {
            int carryBit = FlagC ? 1 : 0;
            FlagZ = false;
            FlagN = false;
            FlagH = false;
            FlagC = (a >> 7) == 1;
            a = ((a << 1) & 0xFF) | carryBit;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_RRA()
        {
            int carryBit = FlagC ? 0x80 : 0x00;
            FlagZ = false;
            FlagN = false;
            FlagH = false;
            FlagC = (a & 1) == 1;
            a = (a >> 1) | carryBit;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_CPL()
        {
            FlagH = true;
            FlagN = true;
            a ^= 0xFF;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_AND_R8(int ra)
        {
            a &= ra;
            FlagZ = a == 0;
            FlagN = false;
            FlagH = true;
            FlagC = false;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_AND_PR16(int rah, int ral)
        {
            a &= ReadByte((rah << 8) + ral);
            FlagZ = a == 0;
            FlagN = false;
            FlagH = true;
            FlagC = false;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_AND_D8()
        {
            a &= ReadByte(pc + 1);
            FlagZ = a == 0;
            FlagN = false;
            FlagH = true;
            FlagC = false;
            pc += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_XOR_R8(int ra)
        {
            a ^= ra;
            FlagZ = a == 0;
            FlagN = false;
            FlagH = false;
            FlagC = false;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_XOR_PR16(int rah, int ral)
        {
            a ^= ReadByte((rah << 8) + ral);
            FlagZ = a == 0;
            FlagN = false;
            FlagH = false;
            FlagC = false;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_XOR_D8()
        {
            a ^= ReadByte(pc + 1);
            FlagZ = a == 0;
            FlagN = false;
            FlagH = false;
            FlagC = false;
            pc += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_OR_R8(int ra)
        {
            a |= ra;
            FlagZ = a == 0;
            FlagN = false;
            FlagH = false;
            FlagC = false;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_OR_PR16(int rah, int ral)
        {
            a |= ReadByte((rah << 8) + ral);
            FlagZ = a == 0;
            FlagN = false;
            FlagH = false;
            FlagC = false;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_OR_D8()
        {
            a |= ReadByte(pc + 1);
            FlagZ = a == 0;
            FlagN = false;
            FlagH = false;
            FlagC = false;
            pc += 2;
        }

        #endregion

        #region Flags

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_SCF()
        {
            FlagN = false;
            FlagH = false;
            FlagC = true;
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_CCF()
        {
            FlagC = !FlagC;
            FlagH = false;
            FlagN = false;
            pc++;
        }

        #endregion

        #region Special

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_EXTENDED()
        {
            int op = ReadByte(pc);
            opcode2[op]();
            cycleamountext = cyclesextended[op];
            pc++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_DAA()
        {
            uint tmp = (uint)a;
            if (FlagC) tmp |= 256;
            if (FlagH) tmp |= 512;
            if (FlagN) tmp |= 1024;
            tmp = daatable[tmp];
            a = (int)(tmp >> 8);
            f = (int)(tmp & 0xFF);
            wroteflagreg = true;
            pc++;
        }

        #endregion

        #region Prefix'd operations

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_RLC_R8(ref int r)
        {
            int highBit = r >> 7;
            FlagN = false;
            FlagH = false;
            FlagC = highBit == 1;
            r = ((r << 1) & 0xFF) | highBit;
            FlagZ = r == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_RLC_PR16(int rh, int rl)
        {
            int address = (rh << 8) + rl;
            int v = ReadByte(address);
            int highBit = v >> 7;
            FlagN = false;
            FlagH = false;
            FlagC = highBit == 1;
            v = ((v << 1) & 0xFF) | highBit;
            FlagZ = v == 0;
            WriteByte(address, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_RRC_R8(ref int r)
        {
            int lowBit = r & 1;
            FlagN = false;
            FlagH = false;
            FlagC = lowBit == 1;
            r = (r >> 1) | (lowBit << 7);
            FlagZ = r == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_RRC_PR16(int rh, int rl)
        {
            int address = (rh << 8) + rl;
            int v = ReadByte(address);
            int lowBit = v & 1;
            FlagN = false;
            FlagH = false;
            FlagC = lowBit == 1;
            v = (v >> 1) | (lowBit << 7);
            FlagZ = v == 0;
            WriteByte(address, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_RL_R8(ref int r)
        {
            int carryBit = FlagC ? 1 : 0;
            FlagN = false;
            FlagH = false;
            FlagC = (r >> 7) == 1;
            r = ((r << 1) & 0xFF) | carryBit;
            FlagZ = r == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_RL_PR16(int rh, int rl)
        {
            int address = (rh << 8) + rl;
            int v = ReadByte(address);
            int carryBit = FlagC ? 1 : 0;
            FlagN = false;
            FlagH = false;
            FlagC = (v >> 7) == 1;
            v = ((v << 1) & 0xFF) | carryBit;
            FlagZ = v == 0;
            WriteByte(address, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_RR_R8(ref int r)
        {
            int carryBit = FlagC ? 0x80 : 0x00;
            FlagN = false;
            FlagH = false;
            FlagC = (r & 1) == 1;
            r = (r >> 1) | carryBit;
            FlagZ = r == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_RR_PR16(int rh, int rl)
        {
            int address = (rh << 8) + rl;
            int v = ReadByte(address);
            int carryBit = FlagC ? 0x80 : 0x00;
            FlagN = false;
            FlagH = false;
            FlagC = (v & 1) == 1;
            v = (v >> 1) | carryBit;
            FlagZ = v == 0;
            WriteByte(address, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_SLA_R8(ref int r)
        {
            FlagN = false;
            FlagH = false;
            FlagC = (r >> 7) == 1;
            r <<= 1;
            r &= 0xFF;
            FlagZ = r == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_SLA_PR16(int rh, int rl)
        {
            int address = (rh << 8) + rl;
            int v = ReadByte(address);
            FlagN = false;
            FlagH = false;
            FlagC = (v >> 7) == 1;
            v <<= 1;
            v &= 0xFF;
            FlagZ = v == 0;
            WriteByte(address, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_SRA_R8(ref int r)
        {
            FlagN = false;
            FlagH = false;
            FlagC = (r & 0x01) == 1;
            r = (r & 0x80) | r >> 1;
            FlagZ = r == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_SRA_PR16(int rh, int rl)
        {
            int address = (rh << 8) + rl;
            int v = ReadByte(address);
            FlagN = false;
            FlagH = false;
            FlagC = (v & 0x01) == 1;
            v = (v & 0x80) | v >> 1;
            FlagZ = v == 0;
            WriteByte(address, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_SRL_R8(ref int r)
        {
            FlagN = false;
            FlagH = false;
            FlagC = (r & 1) == 1;
            r >>= 1;
            r &= 0xFF;
            FlagZ = r == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_SRL_PR16(int rh, int rl)
        {
            int address = (rh << 8) + rl;
            int v = ReadByte(address);
            FlagN = false;
            FlagH = false;
            FlagC = (v & 1) == 1;
            v >>= 1;
            v &= 0xFF;
            FlagZ = v == 0;
            WriteByte(address, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_SWAP_R8(ref int r)
        {
            r = ((r << 4) | (r >> 4)) & 0xFF;
            FlagZ = r == 0;
            FlagN = false;
            FlagH = false;
            FlagC = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_SWAP_PR16(int rh, int rl)
        {
            int address = (rh << 8) + rl;
            int v = ReadByte(address);
            v = (v << 4) | (v >> 4);
            FlagZ = v == 0;
            FlagN = false;
            FlagH = false;
            FlagC = false;
            WriteByte(address, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_BIT_R8(int n, int r)
        {
            FlagZ = (r & (1 << n)) == 0;
            FlagN = false;
            FlagH = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_BIT_PR16(int n, int rh, int rl)
        {
            FlagZ = (ReadByte((rh << 8) + rl) & (1 << n)) == 0;
            FlagN = false;
            FlagH = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_RES_R8(int n, ref int r)
        {
            r = r & (0xFF - (1 << n));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_RES_PR16(int n, int rh, int rl)
        {
            int address = (rh << 8) + rl;
            int v = ReadByte(address);
            v = v & (0xFF - (1 << n));
            WriteByte(address, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_SET_R8(int n, ref int r)
        {
            r |= (1 << n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OP_SET_PR16(int n, int rh, int rl)
        {
            int address = (rh << 8) + rl;
            int v = ReadByte(address);
            v = v | (1 << n);
            WriteByte(address, v);
        }

        internal byte[] CaptureStatePayload()
        {
            return StatePayload.Write(writer => {
                writer.Write((byte)a);
                writer.Write((byte)f);
                writer.Write((byte)b);
                writer.Write((byte)c);
                writer.Write((byte)d);
                writer.Write((byte)e);
                writer.Write((byte)h);
                writer.Write((byte)l);
                writer.Write((ushort)sp);
                writer.Write((ushort)pc);
                writer.Write(Running);
                writer.Write(FlagZ);
                writer.Write(FlagN);
                writer.Write(FlagH);
                writer.Write(FlagC);
                writer.Write(wroteflagreg);
                writer.Write(IME);
                writer.Write(WaitForInterrupt);
                writer.Write(IsStopped);
                writer.Write(IsLockedUp);
                writer.Write(imeEnableDelay);
                writer.Write(haltBug);
                writer.Write(branched);
                writer.Write(cycleamountext);
                writer.Write(PrepareSpeedSwitch);
                writer.Write(IsDoubleSpeed);
            });
        }

        internal Action PrepareStateRestore(byte[] payload)
        {
            return StatePayload.Read(payload, reader => {
                int nextA = reader.ReadByte();
                int nextF = reader.ReadByte() & 0xF0;
                int nextB = reader.ReadByte();
                int nextC = reader.ReadByte();
                int nextD = reader.ReadByte();
                int nextE = reader.ReadByte();
                int nextH = reader.ReadByte();
                int nextL = reader.ReadByte();
                int nextSp = reader.ReadUInt16();
                int nextPc = reader.ReadUInt16();
                bool nextRunning = StatePayload.ReadBoolean(reader);
                bool nextFlagZ = StatePayload.ReadBoolean(reader);
                bool nextFlagN = StatePayload.ReadBoolean(reader);
                bool nextFlagH = StatePayload.ReadBoolean(reader);
                bool nextFlagC = StatePayload.ReadBoolean(reader);
                bool nextWroteFlagRegister = StatePayload.ReadBoolean(reader);
                bool nextIme = StatePayload.ReadBoolean(reader);
                bool nextWaitForInterrupt = StatePayload.ReadBoolean(reader);
                bool nextIsStopped = StatePayload.ReadBoolean(reader);
                bool nextIsLockedUp = StatePayload.ReadBoolean(reader);
                int nextImeEnableDelay = reader.ReadInt32();
                bool nextHaltBug = StatePayload.ReadBoolean(reader);
                bool nextBranched = StatePayload.ReadBoolean(reader);
                int nextExtendedCycles = reader.ReadInt32();
                bool nextPrepareSpeedSwitch = StatePayload.ReadBoolean(reader);
                bool nextDoubleSpeed = StatePayload.ReadBoolean(reader);

                StatePayload.RequireRange(nextImeEnableDelay, 0, 2, nameof(imeEnableDelay));
                StatePayload.RequireRange(nextExtendedCycles, 0, 24, nameof(cycleamountext));

                return (Action)(() => {
                    a = nextA;
                    f = nextF;
                    b = nextB;
                    c = nextC;
                    d = nextD;
                    e = nextE;
                    h = nextH;
                    l = nextL;
                    sp = nextSp;
                    pc = nextPc;
                    Running = nextRunning;
                    FlagZ = nextFlagZ;
                    FlagN = nextFlagN;
                    FlagH = nextFlagH;
                    FlagC = nextFlagC;
                    wroteflagreg = nextWroteFlagRegister;
                    IME = nextIme;
                    WaitForInterrupt = nextWaitForInterrupt;
                    IsStopped = nextIsStopped;
                    IsLockedUp = nextIsLockedUp;
                    imeEnableDelay = nextImeEnableDelay;
                    haltBug = nextHaltBug;
                    branched = nextBranched;
                    cycleamountext = nextExtendedCycles;
                    PrepareSpeedSwitch = nextPrepareSpeedSwitch;
                    IsDoubleSpeed = nextDoubleSpeed;
                });
            });
        }

        #endregion

        #endregion

    }
}

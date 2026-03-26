using System.Collections.Generic;

public static class SolarDataTable
{
    public struct SolarData
    {
        //public string _name;
        public float g; // m/s^2
        public float m; // ton
        public float r; // km
        public SolarData(/*string name, */float g, float m, float r)
        {
            //this._name = name;
            this.g = g;
            this.m = m;
            this.r = r;
        }
    }

    private static readonly Dictionary<SolarObjects, SolarData> table = new()
    {
        { SolarObjects.Sun,      new SolarData(274f,    1.9885e27f,     696_340f) },
        { SolarObjects.Mercury,  new SolarData(3.7f,    3.3011e20f,     2_439.7f) },
        { SolarObjects.Venus,    new SolarData(8.87f,   4.8675e21f,     6_051.8f) },
        { SolarObjects.Earth,    new SolarData(9.81f,   5.97237e21f,    6_371f) },
        { SolarObjects.Mars,     new SolarData(3.72f,   6.4171e20f,     3_389.5f) },
        { SolarObjects.Jupiter,  new SolarData(24.79f,  1.8982e24f,     69_911f) },
        { SolarObjects.Saturn,   new SolarData(10.44f,  5.6834e23f,     58_232f) },
        { SolarObjects.Uranus,   new SolarData(8.69f,   8.6810e22f,     25_362f) },
        { SolarObjects.Neptune,  new SolarData(11.15f,  1.02413e23f,    24_622f) },
        { SolarObjects.Ceres,    new SolarData(0.27f,   9.393e17f,      473f) },
        { SolarObjects.Orcus,    new SolarData(0.3f,    6.3e17f,        460f) },
        { SolarObjects.Pluto,    new SolarData(0.62f,   1.303e19f,      1_188.3f) },
        { SolarObjects.Haumea,   new SolarData(0.44f,   4.006e18f,      816f) },
        { SolarObjects.Quaoar,   new SolarData(0.6f,    1.4e18f,        550f) },
        { SolarObjects.Makemake, new SolarData(0.5f,    3.1e18f,        715f) },
        { SolarObjects.Gonggong, new SolarData(0.6f,    1.75e18f,       600f) },
        { SolarObjects.Eris,     new SolarData(0.82f,   1.66e19f,       1_163f) },
        { SolarObjects.Sedna,    new SolarData(0.4f,    1.0e18f,        500f) },
    };

    public static SolarData? Get(SolarObjects obj)
    {
        if (table.TryGetValue(obj, out var data))
            return data;
        return null;
    }
}

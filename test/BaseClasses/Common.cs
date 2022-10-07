using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace test.BaseClasses
{
    public enum JudgeType
    {
        NA = -1,    // judge not available
        NG = 0,     // ng, out of spec
        NG2 = 1,     // ng, out of spec
        OK = 2,     // ok, in spec
        ERROR,
    }
    /// <summary>
    /// 暂用的保存数据的类型
    /// </summary>
    public enum SaveDataType
    {
        mo,
        glue,
        sc,
        all
    }
    class Common
    {
    }
}

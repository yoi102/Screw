using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaGlue.BaseClasses
{
    public enum JudgeType
    {
        NA = -1,    // judge not available
        NG = 0,     // ng, out of spec
        OK = 1,     // ok, in spec
    }
    /// <summary>
    /// 暂用的保存数据的类型
    /// </summary>
    public enum SaveDataType
    {
        mo,
        gap,
        floating,
        sc,
        all
    }
    class Common
    {
    }
}

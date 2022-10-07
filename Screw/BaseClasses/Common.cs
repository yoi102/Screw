using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Screw.BaseClasses
{
    public enum JudgeType
    {
        NA = -1,    // judge not available
        NG = 0,     // ng, out of spec
        NG2 = 1,     // ng, out of spec
        OK = 2,     // ok, in spec
        ERROR,
    }


    public enum StatusType
    {
        wait ,
        running,
        end,
        error,
    }
    /// <summary>
    /// 暂用的保存数据的类型
    /// </summary>
    public enum SaveDataType
    {
        mo,
        drop3,
        sc,
        all,
        drop12,
        test
    }




    public enum AreaType
    {
        AT = 0,
        CNT = 1,
        TIM = 2
    }

    public enum PointState
    {
        ON = 1,
        OFF = 0
    }

    public enum PLC_Response_Code
    {

        Unknown = -1,
        Command_Completed_Normally = 0,
        Command_Recv_Timeout = 1,
        Command_Send_Error = 2,
        ReadAT_Return_Not_Numeric,
        Set_ResetAT_Response_Failure
    }

    class Common
    {
    }
}

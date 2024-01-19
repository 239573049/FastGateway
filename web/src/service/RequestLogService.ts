import request from "../utils/request";

/***
* 获取请求日志列表
* @param hours {number} 时长（小时）
* @constructor
*/
export function Panel(hours:number){
    return request.get("/api/gateway/panel",{
        params:{
            hours
        }
    });
}
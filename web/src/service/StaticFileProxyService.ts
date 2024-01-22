import { StaticFileProxyEntity } from "../index.d";
import request from "../utils/request";

export function createStaticFileProxy(data:StaticFileProxyEntity){
    return request.post("/api/gateway/static-file-proxy",data);
}

export function updateStaticFileProxy(data:StaticFileProxyEntity){
    return request.put("/api/gateway/static-file-proxy",data);
}

export function deleteStaticFileProxy(id:string){
    return request.delete("/api/gateway/static-file-proxy",{
        params:{
            id
        }
    });
}

export function getStaticFileProxyList(params:any){
    return request.get("/api/gateway/static-file-proxy",{
        params
    });
}

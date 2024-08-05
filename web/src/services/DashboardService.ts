import { get } from "@/utils/fetch"


export const GetDashboard = () =>{
    return get('/api/v1/dashboard')
}
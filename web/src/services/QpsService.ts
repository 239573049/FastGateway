
import { get } from "@/utils/fetch"

export const getQpsData = () => {
    return get('/api/v1/qps')
}
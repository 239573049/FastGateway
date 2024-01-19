import axios from "axios";
import config from "../store/config";

// 创建axios实例
const service = axios.create({
    baseURL: config.API_URL, // api的base_url
    timeout: 240000, // 请求超时时间
});

service.interceptors.request.use((config) => {
    const token = localStorage.getItem("access_token");
    if (token) {
        config.headers["Authorization"] = "Bearer " + token;
    }
    return config;
});

service.interceptors.response.use(
    (response) => {
        if (response.status === 401) {
            localStorage.removeItem("token");
            window.location.href = "/login";
            return Promise.reject("error");
        }
        else {
            return response.data;
        }
    },
    (error) => {
        if(error?.response?.status === 401){
            localStorage.removeItem("token");
            window.location.href = "/login";
            return Promise.reject("error");
        }
        return Promise.reject(error);
    }
);

export default service;
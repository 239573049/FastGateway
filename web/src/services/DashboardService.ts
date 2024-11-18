import { get } from "@/utils/fetch"


export const GetDashboard = () =>{
    return get('/api/v1/dashboard')
}


export async function realtime() {
    // 拼接baseUrl并且处理/重复问题
    const url = `/api/v1/dashboard/realtime`
    const response = await window.fetch(url, {
        method: 'GET',
        headers: {
            'Content-Type': 'application/json',
            'Authorization': "Bearer " + localStorage.getItem('token') || ''
        }
    });

    if (response.status === 401) {
        localStorage.removeItem("access_token");
        window.location.href = "/login";
        return Promise.reject("error");
    }

    
    const reader = response.body!.getReader();

    return {
        [Symbol.asyncIterator]() {
            return {
                async next() {
                    const { done, value } = await reader.read();
                    if (done) {
                        return { done: true, value: null };
                    }
                    const text = new TextDecoder("utf-8").decode(value);

                    const lines = text.split('\n');
                    for (let i = 0; i < lines.length; i++) {
                        const line = lines[i].substring("data:".length);
                        return {
                            done: false,
                            value: JSON.parse(line),
                        };
                    }
                },
            };
        },
    }
}
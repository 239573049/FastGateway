

import { get, postJson } from "@/utils/fetch"

const baseUrl = "/api/v1/authorization";

export interface ApiResponse<T> {
    success: boolean;
    message: string;
    data: T;
}

export interface BotChallengeConfig {
    enabled: boolean;
    configured: boolean;
    siteKey: string;
}

export const getAdminChallengeConfig = (): Promise<ApiResponse<BotChallengeConfig>> => {
    return get(`${baseUrl}/challenge-config`);
};

export interface AdminSecurityStatus {
    usingDefaultPassword: boolean;
}

/** 需登录：是否仍在使用默认密码不能通过匿名接口暴露 */
export const getAdminSecurityStatus = (): Promise<ApiResponse<AdminSecurityStatus>> => {
    return get(`${baseUrl}/security-status`);
};

/** 登录失败过多时，服务端在失败结果的 data 中下发 PoW 挑战 */
export interface PowChallenge {
    state: string;
    challenge: string;
    difficulty: number;
}

export interface PowSolution {
    state: string;
    solution: string;
}

export const Auth = (
    password: string,
    turnstileToken?: string | null,
    pow?: PowSolution | null
): Promise<ApiResponse<string | PowChallenge | null>> => {
    return postJson(baseUrl, {
        password,
        turnstileToken: turnstileToken ?? null,
        riskState: pow?.state ?? null,
        riskSolution: pow?.solution ?? null,
    });
};


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
    /** 管理后台登录是否要求滑块验证 */
    sliderEnabled: boolean;
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

export interface SliderChallenge {
    id: string;
    /** data URL */
    background: string;
    /** 拼图块 data URL，宽为 pieceSize、高与背景一致 */
    piece: string;
    width: number;
    height: number;
    pieceSize: number;
}

/** 拖动轨迹采样点：位移为背景图像素，t 为相对按下时刻的毫秒数 */
export interface SliderTrackPoint {
    x: number;
    y: number;
    t: number;
}

export const getSliderChallenge = (): Promise<ApiResponse<SliderChallenge>> => {
    return get(`${baseUrl}/slider`);
};

/** 验证通过时 data 为一次性登录票据 */
export const verifySlider = (
    id: string,
    x: number,
    track: SliderTrackPoint[]
): Promise<ApiResponse<string | null>> => {
    return postJson(`${baseUrl}/slider`, { id, x, track });
};

export const Auth = (
    password: string,
    turnstileToken?: string | null,
    pow?: PowSolution | null,
    sliderTicket?: string | null
): Promise<ApiResponse<string | PowChallenge | null>> => {
    return postJson(baseUrl, {
        password,
        turnstileToken: turnstileToken ?? null,
        riskState: pow?.state ?? null,
        riskSolution: pow?.solution ?? null,
        sliderTicket: sliderTicket ?? null,
    });
};
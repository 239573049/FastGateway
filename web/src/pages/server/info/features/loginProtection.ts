import { LoginEndpointRule, RiskBlockScope, RiskProtection } from "@/types";

const HEADER_NAME = /^[A-Za-z0-9-]{1,64}$/;

export const LOGIN_METHODS = ["POST", "PUT", "PATCH", "GET"];

export const createLoginEndpoint = (): LoginEndpointRule => ({
    method: "POST",
    path: "",
    usernameField: "",
    failureStatusCodes: [401, 403],
    resultHeader: "",
});

export const createRiskProtection = (): RiskProtection => ({
    enable: true,
    blockScope: RiskBlockScope.LoginOnly,
    loginEndpoints: [createLoginEndpoint()],
});

export const parseStatusCodes = (text: string): number[] =>
    Array.from(
        new Set(
            text
                .split(/[\s,，]+/)
                .filter(Boolean)
                .map((x) => Number(x))
                .filter((x) => Number.isInteger(x))
        )
    );

/** 与后端 RiskProtectionValidator 保持一致，保存前先在前端拦下明显错误 */
export function validateRiskProtection(protection?: RiskProtection | null): string | null {
    if (!protection) return null;
    if (protection.enable && protection.loginEndpoints.length === 0) {
        return "启用登录保护时至少需要配置一个登录接口";
    }

    for (const rule of protection.loginEndpoints) {
        const path = rule.path.trim();
        if (!path.startsWith("/") || path.includes("?")) {
            return "登录接口路径必须以 / 开头，且不能包含查询字符串";
        }
        if (rule.failureStatusCodes.some((x) => x < 100 || x > 599)) {
            return "失败状态码需在 100-599 之间";
        }
        const header = rule.resultHeader?.trim() ?? "";
        if (header && !HEADER_NAME.test(header)) {
            return "结果响应头名称只能包含字母、数字和连字符";
        }
        if (rule.failureStatusCodes.length === 0 && !header) {
            return `登录接口 ${path} 需要配置失败状态码或结果响应头`;
        }
        if (/\s/.test(rule.usernameField?.trim() ?? "")) {
            return "用户名字段不能包含空白";
        }
    }

    return null;
}

export function normalizeRiskProtection(protection?: RiskProtection | null): RiskProtection | null {
    if (!protection) return null;
    if (!protection.enable && protection.loginEndpoints.length === 0) return null;

    return {
        ...protection,
        loginEndpoints: protection.loginEndpoints.map((rule) => ({
            method: rule.method.trim().toUpperCase(),
            path: rule.path.trim(),
            usernameField: rule.usernameField?.trim() || null,
            failureStatusCodes: rule.failureStatusCodes,
            resultHeader: rule.resultHeader?.trim() || null,
        })),
    };
}

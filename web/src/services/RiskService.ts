import { del, get, postJson, putJson } from "@/utils/fetch";
import type {
  RiskDecision,
  RiskModelEvaluation,
  RiskModelInfo,
  RiskModelProvider,
  RiskModelProviderInput,
  RiskModelStatus,
  RiskModelTestResult,
  RiskSettings,
  RiskTag,
} from "@/types";

const baseUrl = "/api/v1/risk";

interface RiskResponse<T> {
  success: boolean;
  message?: string;
  data: T;
}

export const getRiskTags = (): Promise<RiskResponse<RiskTag[]>> => {
  return get(`${baseUrl}/tags`);
};

export const removeRiskTag = (id: string): Promise<RiskResponse<null>> => {
  return del(`${baseUrl}/tags?id=${encodeURIComponent(id)}`);
};

export const getRiskDecisions = (): Promise<RiskResponse<RiskDecision[]>> => {
  return get(`${baseUrl}/decisions`);
};

export const getRiskSettings = (): Promise<RiskResponse<RiskSettings>> => {
  return get(`${baseUrl}/settings`);
};

export const updateRiskSettings = (settings: RiskSettings): Promise<RiskResponse<null>> => {
  return putJson(`${baseUrl}/settings`, settings);
};

export const getRiskModelProvider = (): Promise<RiskResponse<RiskModelProvider>> => {
  return get(`${baseUrl}/model-provider`);
};

export const updateRiskModelProvider = (
  input: RiskModelProviderInput
): Promise<RiskResponse<RiskModelProvider | null>> => {
  return putJson(`${baseUrl}/model-provider`, input);
};

export const testRiskModelProvider = (
  input: RiskModelProviderInput
): Promise<RiskResponse<RiskModelTestResult | null>> => {
  return postJson(`${baseUrl}/model-provider/test`, input);
};

export const listRiskModels = (input: RiskModelProviderInput): Promise<RiskResponse<RiskModelInfo[] | null>> => {
  return postJson(`${baseUrl}/model-provider/models`, input);
};

export const getRiskModelStatus = (): Promise<RiskResponse<RiskModelStatus>> => {
  return get(`${baseUrl}/model-status`);
};

export const getRiskModelEvaluations = (): Promise<RiskResponse<RiskModelEvaluation[]>> => {
  return get(`${baseUrl}/model-evaluations`);
};

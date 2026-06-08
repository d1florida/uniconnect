import type { PasswordPolicyDto } from '../api/types';

export function formatPasswordPolicy(policy: PasswordPolicyDto): string {
  const rules = [`at least ${policy.minLength} characters`];
  if (policy.requireDigit) rules.push('one digit');
  if (policy.requireUppercase) rules.push('one uppercase letter');
  if (policy.requireLowercase) rules.push('one lowercase letter');
  if (policy.requireNonAlphanumeric) rules.push('one special character');
  return rules.join(', ');
}

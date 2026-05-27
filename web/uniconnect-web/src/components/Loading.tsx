export function Loading({ message = 'Loading…' }: { message?: string }) {
  return <p className="loading">{message}</p>;
}

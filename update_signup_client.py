import re

with open('./web/src/Apps/JSini.Web.Admin/Api/SignupClient.cs', 'r') as f:
    content = f.read()

merge_method = '''
    public Task MergeAsync(string pendingId, string targetId, CancellationToken ct = default)
        => gateway.PostAsync(
            $"auth/system/signup/{pendingId}/merge?targetId={targetId}",
            null, ct);
'''

content = content.replace(
    '            null, ct);',
    '            null, ct);' + merge_method
)

with open('./web/src/Apps/JSini.Web.Admin/Api/SignupClient.cs', 'w') as f:
    f.write(content)

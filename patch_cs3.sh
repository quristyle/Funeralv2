sed -i -e '/if (!string.IsNullOrWhiteSpace(_adminId))/i \
            if (!string.IsNullOrWhiteSpace(_requesterId))\
            {\
                query["customerId"] = _requesterId;\
            }\
' ./web/src/Apps/JSini.Web.HelpDesk/Components/Pages/RequestManage.razor.cs

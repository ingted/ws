(function()
{
 var Global=this,Runtime=this.IntelliFactory.Runtime,WebSocket,Concurrency,Owin,WebSocket1,Client,Control,MailboxProcessor,List,T,Json,JSON,Exception,Unchecked,Endpoint;
 Runtime.Define(Global,{
  WebSharper:{
   Owin:{
    WebSocket:{
     Client:{
      ConnectTo:function(endpoint,agent)
      {
       var socket,proc,internalServer,server;
       socket=new WebSocket(endpoint.URI);
       proc=function(msg)
       {
        return Concurrency.Start(Client.processResponse(socket,agent.PostAndAsyncReply(function(chan)
        {
         return[msg,chan];
        },{
         $:0
        })),{
         $:0
        });
       };
       internalServer=MailboxProcessor.Start(function(inbox)
       {
        var loop;
        loop=function(st)
        {
         return Concurrency.Delay(function()
         {
          var x;
          x=inbox.Receive({
           $:0
          });
          return Concurrency.Bind(x,function(_arg1)
          {
           var msg;
           if(_arg1.$==1)
            {
             return loop({
              State:{
               $:1
              },
              Queue:st.Queue
             });
            }
           else
            {
             if(_arg1.$==2)
              {
               msg=_arg1.$0;
               return st.State.$==1?loop({
                State:st.State,
                Queue:Runtime.New(T,{
                 $:1,
                 $0:msg,
                 $1:st.Queue
                })
               }):Concurrency.Bind(Client.processResponse(socket,Concurrency.Return(msg)),function()
               {
                return loop(st);
               });
              }
             else
              {
               return Concurrency.Combine(Concurrency.For(st.Queue,function(_arg2)
               {
                return Concurrency.Bind(Client.processResponse(socket,Concurrency.Return(_arg2)),function()
                {
                 return Concurrency.Return(null);
                });
               }),Concurrency.Delay(function()
               {
                return loop({
                 State:{
                  $:0
                 },
                 Queue:Runtime.New(T,{
                  $:0
                 })
                });
               }));
              }
            }
          });
         });
        };
        return loop({
         State:{
          $:1
         },
         Queue:Runtime.New(T,{
          $:0
         })
        });
       },{
        $:0
       });
       server=MailboxProcessor.Start(function(inbox)
       {
        var loop;
        loop=function()
        {
         return Concurrency.Delay(function()
         {
          return Concurrency.Bind(inbox.Receive({
           $:0
          }),function(_arg5)
          {
           internalServer.mailbox.AddLast({
            $:2,
            $0:_arg5
           });
           internalServer.resume();
           return loop(null);
          });
         });
        };
        return loop(null);
       },{
        $:0
       });
       socket.onopen=function()
       {
        internalServer.mailbox.AddLast({
         $:0
        });
        internalServer.resume();
        return proc({
         $:2,
         $0:server
        });
       };
       socket.onclose=function()
       {
        internalServer.mailbox.AddLast({
         $:1
        });
        internalServer.resume();
        return proc({
         $:3
        });
       };
       socket.onmessage=function(msg)
       {
        return proc({
         $:0,
         $0:Json.Activate(JSON.parse(msg.data))
        });
       };
       socket.onerror=function()
       {
        return proc({
         $:1,
         $0:Exception.New1("error")
        });
       };
       return server;
      },
      processResponse:function(sock,msg)
      {
       return Concurrency.Delay(function()
       {
        return Concurrency.Combine(Concurrency.While(function()
        {
         return!Unchecked.Equals(sock.readyState,1);
        },Concurrency.Delay(function()
        {
         return Concurrency.Bind(Concurrency.Sleep(10),function()
         {
          return Concurrency.Return(null);
         });
        })),Concurrency.Delay(function()
        {
         return Concurrency.Bind(msg,function(_arg2)
         {
          if(_arg2.$==1)
           {
            sock.close();
            return Concurrency.Return(null);
           }
          else
           {
            sock.send(JSON.stringify(_arg2.$0));
            return Concurrency.Return(null);
           }
         });
        }));
       });
      }
     },
     Endpoint:Runtime.Class({},{
      FromUri:function(uri)
      {
       return Runtime.New(Endpoint,{
        URI:uri
       });
      }
     })
    }
   }
  }
 });
 Runtime.OnInit(function()
 {
  WebSocket=Runtime.Safe(Global.WebSocket);
  Concurrency=Runtime.Safe(Global.WebSharper.Concurrency);
  Owin=Runtime.Safe(Global.WebSharper.Owin);
  WebSocket1=Runtime.Safe(Owin.WebSocket);
  Client=Runtime.Safe(WebSocket1.Client);
  Control=Runtime.Safe(Global.WebSharper.Control);
  MailboxProcessor=Runtime.Safe(Control.MailboxProcessor);
  List=Runtime.Safe(Global.WebSharper.List);
  T=Runtime.Safe(List.T);
  Json=Runtime.Safe(Global.WebSharper.Json);
  JSON=Runtime.Safe(Global.JSON);
  Exception=Runtime.Safe(Global.WebSharper.Exception);
  Unchecked=Runtime.Safe(Global.WebSharper.Unchecked);
  return Endpoint=Runtime.Safe(WebSocket1.Endpoint);
 });
 Runtime.OnLoad(function()
 {
  return;
 });
}());

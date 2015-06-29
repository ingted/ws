(function()
{
 var Global=this,Runtime=this.IntelliFactory.Runtime,WebSocket,Concurrency,Owin,WebSocket1,Client,Control,MailboxProcessor,Collections,ResizeArray,ResizeArrayProxy,Json,JSON,Exception,Unchecked;
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
               if(st.State.$==1)
                {
                 st.Queue.Add(msg);
                 return loop(st);
                }
               else
                {
                 return Concurrency.Bind(Client.processResponse(socket,Concurrency.Return(msg)),function()
                 {
                  return loop(st);
                 });
                }
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
                st.Queue.Clear();
                return loop({
                 State:{
                  $:0
                 },
                 Queue:st.Queue
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
         Queue:ResizeArrayProxy.New2()
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
     }
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
  Collections=Runtime.Safe(Global.WebSharper.Collections);
  ResizeArray=Runtime.Safe(Collections.ResizeArray);
  ResizeArrayProxy=Runtime.Safe(ResizeArray.ResizeArrayProxy);
  Json=Runtime.Safe(Global.WebSharper.Json);
  JSON=Runtime.Safe(Global.JSON);
  Exception=Runtime.Safe(Global.WebSharper.Exception);
  return Unchecked=Runtime.Safe(Global.WebSharper.Unchecked);
 });
 Runtime.OnLoad(function()
 {
  return;
 });
}());
